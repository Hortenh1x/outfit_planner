import { type ChangeEvent, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { GitBranch, Layers3, Link2, Plus, ScanFace, Sparkles, Trash2, Wand2 } from 'lucide-react';
import { createBodyReferencePhoto, createGarment, createOutfit, deleteBodyReferencePhoto, deleteOutfit, deleteOutfitTryOnPreview, deleteTryOnJobOutput, estimateTryOn, getTryOnJob, listBodyReferencePhotos, listGarments, listOutfits, shareOutfit, startTryOn, updateOutfit, uploadBodyReferencePhoto, uploadGarmentPhoto } from '../api/client';
import { ModeToggle } from '../components/ModeToggle';
import { BodyReferenceManager } from '../features/builder/BodyReferenceManager';
import { garmentNameFromFile } from '../features/builder/garmentName';
import { OutfitList } from '../features/builder/OutfitList';
import { SlotPicker } from '../features/builder/SlotPicker';
import { useAuthSession } from '../features/auth/authQueries';
import { CATEGORY_SELECTION_KEYS, GARMENT_CATEGORIES, groupGarmentsByCategory, selectedGarmentIds, selectionLabel } from '../features/outfits/outfitUtils';
import { creditsLabel, modeLabel } from '../features/tryon/tryOnText';
import { garmentPhotoUrlsFromUpload } from '../features/uploads/uploadedPhotoUrls';
import { validateUploadImageFile } from '../features/uploads/imageFile';
import { EmptyPreview } from '../shared/ui/EmptyPreview';
import { PanelTitle } from '../shared/ui/PanelTitle';
import { PanelSkeleton } from '../shared/ui/Skeletons';
import type { GarmentCategory, Outfit, OutfitSelection, PreviewMode, TryOnCostEstimate, TryOnMode } from '../types';

const tryOnJobPollIntervalMs = 1000;

export function BuilderPage() {
  const queryClient = useQueryClient();
  const garmentsQuery = useQuery({ queryKey: ['garments'], queryFn: listGarments });
  const outfitsQuery = useQuery({ queryKey: ['outfits'], queryFn: listOutfits });
  const bodyPhotosQuery = useQuery({ queryKey: ['body-reference-photos'], queryFn: listBodyReferencePhotos });
  const sessionQuery = useAuthSession();
  const garments = garmentsQuery.data ?? [];
  const grouped = groupGarmentsByCategory(garments);
  const [selection, setSelection] = useState<OutfitSelection>({});
  const [mode, setMode] = useState<PreviewMode>('clothes');
  const [outfitName, setOutfitName] = useState('Today');
  const [selectedBodyPhotoId, setSelectedBodyPhotoId] = useState('');
  const [bodyPhotoUploadError, setBodyPhotoUploadError] = useState<string | null>(null);
  const [quickAddGarmentError, setQuickAddGarmentError] = useState<string | null>(null);
  const [tryOnMode, setTryOnMode] = useState<TryOnMode>('SequentialOutfitTryOn');
  const [pendingEstimate, setPendingEstimate] = useState<TryOnCostEstimate | null>(null);
  const [activeOutfit, setActiveOutfit] = useState<Outfit | null>(null);
  const selectedIds = selectedGarmentIds(selection);
  const activeOutfitHasChanges = activeOutfit
    ? outfitName !== activeOutfit.name || !sameIds(selectedIds, activeOutfit.items.map((item) => item.garmentId))
    : false;

  const saveMutation = useMutation({
    mutationFn: () => {
      const input = { name: outfitName, garmentIds: selectedIds };
      return activeOutfit ? updateOutfit(activeOutfit.id, input) : createOutfit(input);
    },
    onSuccess: (outfit) => {
      setActiveOutfit(outfit);
      void queryClient.invalidateQueries({ queryKey: ['outfits'] });
    }
  });
  const tryOnMutation = useMutation({
    mutationFn: startTryOn,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['outfits'] });
    }
  });
  const estimateMutation = useMutation({ mutationFn: estimateTryOn });
  const tryOnJobQuery = useQuery({
    queryKey: ['try-on-job', tryOnMutation.data?.id],
    queryFn: () => getTryOnJob(tryOnMutation.data?.id ?? ''),
    enabled: Boolean(tryOnMutation.data?.id),
    refetchInterval: (query) => {
      const job = query.state.data;
      return job?.status === 'Queued' || job?.status === 'Processing' ? tryOnJobPollIntervalMs : false;
    }
  });
  const bodyPhotoUploadMutation = useMutation({
    mutationFn: async (file: File) => {
      validateUploadImageFile(file);

      const uploaded = await uploadBodyReferencePhoto(file);
      return await createBodyReferencePhoto(uploaded.url);
    },
    onSuccess: (photo) => {
      setSelectedBodyPhotoId(photo.id);
      setBodyPhotoUploadError(null);
      void queryClient.invalidateQueries({ queryKey: ['body-reference-photos'] });
    },
    onError: (error) => setBodyPhotoUploadError((error as Error).message)
  });
  const deleteBodyPhotoMutation = useMutation({
    mutationFn: deleteBodyReferencePhoto,
    onSuccess: (_, deletedPhotoId) => {
      if (selectedBodyPhotoId === deletedPhotoId) {
        setSelectedBodyPhotoId('');
      }

      void queryClient.invalidateQueries({ queryKey: ['body-reference-photos'] });
    }
  });
  const quickAddGarmentMutation = useMutation({
    mutationFn: async (input: { category: GarmentCategory; file: File }) => {
      validateUploadImageFile(input.file);

      const uploadedPhoto = await uploadGarmentPhoto(input.file);
      const photoUrls = garmentPhotoUrlsFromUpload(uploadedPhoto);
      return await createGarment({
        name: garmentNameFromFile(input.file, input.category),
        category: input.category,
        imageUrl: photoUrls.imageUrl,
        thumbnailUrl: photoUrls.thumbnailUrl,
        tags: []
      });
    },
    onSuccess: (garment) => {
      setSelection((current) => ({
        ...current,
        [CATEGORY_SELECTION_KEYS[garment.category]]: garment.id
      }));
      setActiveOutfit(null);
      setQuickAddGarmentError(null);
      void queryClient.invalidateQueries({ queryKey: ['garments'] });
    },
    onError: (error) => setQuickAddGarmentError((error as Error).message)
  });
  const shareMutation = useMutation({ mutationFn: shareOutfit });
  const deleteOutfitMutation = useMutation({
    mutationFn: deleteOutfit,
    onSuccess: (_, deletedOutfitId) => {
      if (activeOutfit?.id === deletedOutfitId) {
        setActiveOutfit(null);
        setPendingEstimate(null);
        estimateMutation.reset();
        tryOnMutation.reset();
        shareMutation.reset();
        setMode('clothes');
      }

      void queryClient.invalidateQueries({ queryKey: ['outfits'] });
      void queryClient.invalidateQueries({ queryKey: ['schedule'] });
    }
  });
  const selectedGarments = garments.filter((garment) => selectedIds.includes(garment.id));
  const latestTryOnJob = tryOnJobQuery.data ?? tryOnMutation.data;
  const activeOutfitPreviewUrl = activeOutfit?.personPreviewUrl;
  const previewUrl = latestTryOnJob?.outputImageUrl ?? activeOutfitPreviewUrl;
  const canDeletePreview = Boolean(latestTryOnJob?.outputImageUrl || activeOutfitPreviewUrl);
  const deletePreviewMutation = useMutation({
    mutationFn: (input: { jobId?: string; outfitId?: string }) => {
      if (input.jobId) {
        return deleteTryOnJobOutput(input.jobId);
      }

      if (!input.outfitId) {
        throw new Error('No preview selected for deletion.');
      }

      return deleteOutfitTryOnPreview(input.outfitId);
    },
    onSuccess: () => {
      const deletedOutputUrl = latestTryOnJob?.outputImageUrl ?? activeOutfitPreviewUrl;
      setActiveOutfit((current) => current && current.personPreviewUrl === deletedOutputUrl
        ? { ...current, personPreviewUrl: null }
        : current);
      setPendingEstimate(null);
      tryOnMutation.reset();
      setMode('clothes');
      void queryClient.invalidateQueries({ queryKey: ['outfits'] });
    }
  });
  const bodyPhotos = bodyPhotosQuery.data ?? [];
  const selectedBodyPhoto = bodyPhotos.find((photo) => photo.id === selectedBodyPhotoId) ?? bodyPhotos[0];
  const selectedBodyReferenceInput = selectedBodyPhoto?.imageUrl
    ? { bodyReferencePhotoUrl: selectedBodyPhoto.imageUrl, bodyReferencePhotoId: selectedBodyPhoto.id }
    : {};
  const requiresBodyReference = tryOnMode !== 'ClothesOnlyPreview';
  const requiresProfileGender = tryOnMode !== 'ClothesOnlyPreview' && Boolean(sessionQuery.data?.user) && !sessionQuery.data?.user.gender;

  useEffect(() => {
    if (!selectedBodyPhotoId && bodyPhotos.length > 0) {
      setSelectedBodyPhotoId(bodyPhotos[0].id);
      return;
    }

    if (selectedBodyPhotoId && bodyPhotos.length > 0 && !bodyPhotos.some((photo) => photo.id === selectedBodyPhotoId)) {
      setSelectedBodyPhotoId(bodyPhotos[0].id);
      return;
    }

    if (selectedBodyPhotoId && bodyPhotos.length === 0) {
      setSelectedBodyPhotoId('');
      setPendingEstimate(null);
    }
  }, [bodyPhotos, selectedBodyPhotoId]);

  useEffect(() => {
    if (latestTryOnJob?.status === 'Succeeded') {
      void queryClient.invalidateQueries({ queryKey: ['outfits'] });
    }
  }, [latestTryOnJob?.status, latestTryOnJob?.outputImageUrl, queryClient]);

  async function ensureOutfit() {
    if (activeOutfit && !activeOutfitHasChanges) {
      return activeOutfit;
    }

    return await saveMutation.mutateAsync();
  }

  function handleBodyPhotoFileChange(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    event.target.value = '';
    if (file) {
      bodyPhotoUploadMutation.mutate(file);
    }
  }

  function handleQuickAddGarment(category: GarmentCategory, event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    event.target.value = '';
    if (file) {
      quickAddGarmentMutation.mutate({ category, file });
    }
  }

  function updateSelection(selectionKey: keyof OutfitSelection, id: string) {
    setSelection((current) => {
      if (current[selectionKey] === id) {
        return current;
      }

      return { ...current, [selectionKey]: id };
    });

    if (selection[selectionKey] !== id) {
      setPendingEstimate(null);
      estimateMutation.reset();
      tryOnMutation.reset();
      shareMutation.reset();
    }
  }

  function handlePickOutfit(outfit: Outfit) {
    setActiveOutfit(outfit);
    setSelection(selectionFromOutfit(outfit));
    setOutfitName(outfit.name);
    setPendingEstimate(null);
    estimateMutation.reset();
    tryOnMutation.reset();
    setMode(outfit.personPreviewUrl ? 'person' : 'clothes');
  }

  return (
    <section className="builder-editorial-page">
      <aside className="builder-wardrobe-rail">
        <PanelTitle icon={<Layers3 size={19} />} title="Wardrobe pieces" />
        {garmentsQuery.isLoading ? (
          <PanelSkeleton />
        ) : (
          <>
            {GARMENT_CATEGORIES.map((category) => {
              const selectionKey = CATEGORY_SELECTION_KEYS[category];
              return (
                <SlotPicker
                  key={category}
                  title={category}
                  category={category}
                  garments={grouped[category]}
                  selectedId={selection[selectionKey]}
                  onSelect={(id) => updateSelection(selectionKey, id)}
                  onQuickAdd={(event) => handleQuickAddGarment(category, event)}
                  isQuickAdding={quickAddGarmentMutation.isPending && quickAddGarmentMutation.variables?.category === category}
                />
              );
            })}
          </>
        )}
      </aside>

      <div className="preview-stage">
        <header className="builder-hero">
          <div>
            <p>Builder</p>
            <h1>Build looks with <em>intention.</em></h1>
          </div>
          <span>{selectionLabel(selection, garments)}</span>
          <ModeToggle mode={mode} onChange={setMode} />
        </header>

        <div className="preview-canvas builder-preview">
          {mode === 'clothes' ? (
            <div className="clothes-stack">
              {selectedGarments.length === 0 ? <EmptyPreview /> : null}
              {selectedGarments.map((garment, index) => (
                <img
                  key={garment.id}
                  src={garment.thumbnailUrl}
                  alt={garment.name}
                  className={index === 0 ? 'tilt-left' : 'tilt-right'}
                />
              ))}
            </div>
          ) : (
            <div className="person-preview">
              {previewUrl ? <img src={previewUrl} alt="Generated try-on preview" /> : <EmptyPreview />}
            </div>
          )}
        </div>
      </div>

      <aside className="builder-details-rail builder-controls">
        <PanelTitle icon={<Wand2 size={19} />} title="Outfit controls" />
        <div className="stack">
          <BodyReferenceManager
            photos={bodyPhotos}
            selectedPhoto={selectedBodyPhoto}
            isLoading={bodyPhotosQuery.isLoading}
            deletingId={deleteBodyPhotoMutation.isPending ? deleteBodyPhotoMutation.variables : undefined}
            onSelect={(id) => {
              setSelectedBodyPhotoId(id);
              setPendingEstimate(null);
            }}
            onDelete={(id) => deleteBodyPhotoMutation.mutate(id)}
            onUpload={handleBodyPhotoFileChange}
          />
          <div className="tryon-mode-selector" role="group" aria-label="Try-on mode">
            {(['ClothesOnlyPreview', 'SingleGarmentTryOn', 'SequentialOutfitTryOn', 'ExperimentalCompositeTryOn'] as TryOnMode[]).map((option) => (
              <button
                key={option}
                type="button"
                className={tryOnMode === option ? 'flow-toggle active' : 'flow-toggle'}
                aria-pressed={tryOnMode === option}
                onClick={() => {
                  setTryOnMode(option);
                  setPendingEstimate(null);
                }}
              >
                <GitBranch size={16} />
                <span>{modeLabel(option)}</span>
              </button>
            ))}
          </div>
          <button
            type="button"
            className="primary-action generate-action"
            disabled={selectedIds.length === 0 || requiresProfileGender || (requiresBodyReference && !selectedBodyPhoto?.imageUrl) || estimateMutation.isPending}
            onClick={async () => {
              const outfit = await ensureOutfit();
              const estimate = await estimateMutation.mutateAsync({
                outfitId: outfit.id,
                ...selectedBodyReferenceInput,
                tryOnMode
              });
              setPendingEstimate(estimate);
            }}
          >
            <Sparkles size={16} />
            {estimateMutation.isPending ? 'Estimating' : 'Generate preview'}
          </button>
          {requiresProfileGender ? <p className="error">Set gender in account settings before using AI try-on.</p> : null}
          {pendingEstimate ? (
            <div className="tryon-confirmation">
              <div>
                <small>{modeLabel(pendingEstimate.mode)}</small>
                <strong>{creditsLabel(pendingEstimate.estimatedCredits)}</strong>
                <p>{pendingEstimate.summary}</p>
              </div>
              {pendingEstimate.hasCachedResult ? <p>Cached result available</p> : null}
              {pendingEstimate.bodyTryOnItems.length > 0 ? (
                <p>Included: {pendingEstimate.bodyTryOnItems.map((item) => item.name).join(', ')}</p>
              ) : null}
              {pendingEstimate.visualOnlyItems.length > 0 ? (
                <p>Visual-only: {pendingEstimate.visualOnlyItems.map((item) => item.name).join(', ')}</p>
              ) : null}
              {pendingEstimate.warnings.map((warning) => (
                <p className="error" key={warning}>{warning}</p>
              ))}
              <button
                type="button"
                className="primary-action"
                disabled={!pendingEstimate.isAvailable || (pendingEstimate.requiresAi && (requiresProfileGender || !selectedBodyPhoto?.imageUrl)) || tryOnMutation.isPending}
                onClick={async () => {
                  const outfit = await ensureOutfit();
                  await tryOnMutation.mutateAsync({
                    outfitId: outfit.id,
                    ...selectedBodyReferenceInput,
                    consentAccepted: pendingEstimate.requiresAi,
                    tryOnMode: pendingEstimate.mode,
                    confirmedCredits: pendingEstimate.estimatedCredits,
                    confirmedCacheKey: pendingEstimate.cacheKey
                  });
                  setPendingEstimate(null);
                  setMode('person');
                }}
              >
                <Sparkles size={16} />
                {tryOnMutation.isPending ? 'Generating' : 'Confirm generation'}
              </button>
            </div>
          ) : null}
          {latestTryOnJob ? (
            <div className="tryon-status">
              <ScanFace size={17} />
              <div>
                <small>Try-on job</small>
                <strong>{latestTryOnJob.status}</strong>
              </div>
            </div>
          ) : null}
          {canDeletePreview ? (
            <button
              type="button"
              className="secondary-action danger-action"
              disabled={deletePreviewMutation.isPending}
              onClick={() => deletePreviewMutation.mutate(
                latestTryOnJob?.id && latestTryOnJob.outputImageUrl
                  ? { jobId: latestTryOnJob.id }
                  : { outfitId: activeOutfit?.id }
              )}
            >
              <Trash2 size={16} />
              {deletePreviewMutation.isPending ? 'Deleting preview' : 'Delete preview'}
            </button>
          ) : null}
          <button
            type="button"
            className="secondary-action"
            disabled={!activeOutfit || activeOutfitHasChanges || shareMutation.isPending}
            onClick={() => activeOutfit && shareMutation.mutate(activeOutfit.id)}
          >
            <Link2 size={16} />
            Share
          </button>
          {shareMutation.data ? (
            <Link className="share-link" to={shareMutation.data.url}>
              {shareMutation.data.url}
            </Link>
          ) : null}
          <div className="builder-save-block">
            <label>
              <span>Outfit name</span>
              <input
                value={outfitName}
                onChange={(event) => {
                  setOutfitName(event.target.value);
                  setPendingEstimate(null);
                  estimateMutation.reset();
                  tryOnMutation.reset();
                  shareMutation.reset();
                }}
              />
            </label>
            <button
              type="button"
              className="primary-action"
              disabled={selectedIds.length === 0 || saveMutation.isPending}
              onClick={() => saveMutation.mutate()}
            >
              <Plus size={16} />
              {saveMutation.isPending ? 'Saving' : activeOutfit ? 'Save changes' : 'Save outfit'}
            </button>
          </div>
          {activeOutfit ? (
            <button
              type="button"
              className="secondary-action danger-action"
              disabled={deleteOutfitMutation.isPending}
              onClick={() => deleteOutfitMutation.mutate(activeOutfit.id)}
            >
              <Trash2 size={16} />
              {deleteOutfitMutation.isPending ? 'Deleting outfit' : 'Delete outfit'}
            </button>
          ) : null}
          {[quickAddGarmentError ? new Error(quickAddGarmentError) : null, bodyPhotoUploadError ? new Error(bodyPhotoUploadError) : null, saveMutation.error, estimateMutation.error, tryOnMutation.error, shareMutation.error, deleteBodyPhotoMutation.error, deleteOutfitMutation.error, deletePreviewMutation.error, tryOnJobQuery.error].filter(Boolean).map((error) => (
            <p className="error" key={(error as Error).message}>
              {(error as Error).message}
            </p>
          ))}
          <OutfitList outfits={outfitsQuery.data ?? []} onPick={handlePickOutfit} />
        </div>
      </aside>
    </section>
  );
}

function selectionFromOutfit(outfit: Outfit): OutfitSelection {
  return outfit.items.reduce((nextSelection, item) => {
    nextSelection[CATEGORY_SELECTION_KEYS[item.category]] = item.garmentId;
    return nextSelection;
  }, {} as OutfitSelection);
}

function sameIds(left: string[], right: string[]) {
  if (left.length !== right.length) {
    return false;
  }

  const sortedLeft = [...left].sort();
  const sortedRight = [...right].sort();
  return sortedLeft.every((id, index) => id === sortedRight[index]);
}
