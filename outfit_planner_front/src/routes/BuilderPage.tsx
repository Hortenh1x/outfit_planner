import { type ChangeEvent, type CSSProperties, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { GitBranch, Layers3, Link2, Plus, ScanFace, Sparkles, Wand2 } from 'lucide-react';
import { createBodyReferencePhoto, createGarment, createOutfit, deleteBodyReferencePhoto, getTryOnJob, listBodyReferencePhotos, listGarments, listOutfits, shareOutfit, startTryOn, uploadBodyReferencePhoto, uploadGarmentPhoto } from '../api/client';
import { ModeToggle } from '../components/ModeToggle';
import { BodyReferenceManager } from '../features/builder/BodyReferenceManager';
import { garmentNameFromFile } from '../features/builder/garmentName';
import { OutfitList } from '../features/builder/OutfitList';
import { SlotPicker } from '../features/builder/SlotPicker';
import { CATEGORY_SELECTION_KEYS, GARMENT_CATEGORIES, groupGarmentsByCategory, selectedGarmentIds, selectionLabel } from '../features/outfits/outfitUtils';
import { validateUploadImageFile } from '../features/uploads/imageFile';
import { EmptyPreview } from '../shared/ui/EmptyPreview';
import { PanelTitle } from '../shared/ui/PanelTitle';
import { PanelSkeleton } from '../shared/ui/Skeletons';
import type { GarmentCategory, Outfit, OutfitSelection, PreviewMode } from '../types';

const headingStyle: CSSProperties = { fontFamily: 'Nunito, sans-serif' };

export function BuilderPage() {
  const queryClient = useQueryClient();
  const garmentsQuery = useQuery({ queryKey: ['garments'], queryFn: listGarments });
  const outfitsQuery = useQuery({ queryKey: ['outfits'], queryFn: listOutfits });
  const bodyPhotosQuery = useQuery({ queryKey: ['body-reference-photos'], queryFn: listBodyReferencePhotos });
  const garments = garmentsQuery.data ?? [];
  const grouped = groupGarmentsByCategory(garments);
  const [selection, setSelection] = useState<OutfitSelection>({});
  const [mode, setMode] = useState<PreviewMode>('clothes');
  const [outfitName, setOutfitName] = useState('Today');
  const [selectedBodyPhotoId, setSelectedBodyPhotoId] = useState('');
  const [bodyPhotoUploadError, setBodyPhotoUploadError] = useState<string | null>(null);
  const [quickAddGarmentError, setQuickAddGarmentError] = useState<string | null>(null);
  const [sequentialFlowEnabled, setSequentialFlowEnabled] = useState(false);
  const [activeOutfit, setActiveOutfit] = useState<Outfit | null>(null);

  const saveMutation = useMutation({
    mutationFn: createOutfit,
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
  const tryOnJobQuery = useQuery({
    queryKey: ['try-on-job', tryOnMutation.data?.id],
    queryFn: () => getTryOnJob(tryOnMutation.data?.id ?? ''),
    enabled: Boolean(tryOnMutation.data?.id)
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
      return await createGarment({
        name: garmentNameFromFile(input.file, input.category),
        category: input.category,
        imageUrl: uploadedPhoto.url,
        thumbnailUrl: uploadedPhoto.url,
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
  const selectedIds = selectedGarmentIds(selection);
  const selectedGarments = garments.filter((garment) => selectedIds.includes(garment.id));
  const latestTryOnJob = tryOnJobQuery.data ?? tryOnMutation.data;
  const previewUrl = latestTryOnJob?.outputImageUrl ?? activeOutfit?.personPreviewUrl;
  const bodyPhotos = bodyPhotosQuery.data ?? [];
  const selectedBodyPhoto = bodyPhotos.find((photo) => photo.id === selectedBodyPhotoId) ?? bodyPhotos[0];

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
    }
  }, [bodyPhotos, selectedBodyPhotoId]);

  async function ensureOutfit() {
    if (activeOutfit) {
      return activeOutfit;
    }

    return await saveMutation.mutateAsync({ name: outfitName, garmentIds: selectedIds });
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
      setActiveOutfit(null);
    }
  }

  return (
    <section className="builder-layout">
      <aside className="inventory-panel">
        <PanelTitle icon={<Layers3 size={19} />} title="Wardrobe clay" />
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
        <header className="builder-header">
          <div>
            <p>Builder</p>
            <h1 style={headingStyle}>{selectionLabel(selection, garments)}</h1>
          </div>
          <ModeToggle mode={mode} onChange={setMode} />
        </header>

        <div className="preview-canvas">
          <div className="preview-topography" aria-hidden="true">
            <span />
            <span />
            <span />
          </div>
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

      <aside className="tool-panel builder-controls">
        <PanelTitle icon={<Wand2 size={19} />} title="Outfit controls" />
        <div className="stack">
          <label>
            <span>Outfit name</span>
            <input value={outfitName} onChange={(event) => setOutfitName(event.target.value)} />
          </label>
          <button
            type="button"
            className="clay-button primary-action"
            disabled={selectedIds.length === 0 || saveMutation.isPending}
            onClick={() => saveMutation.mutate({ name: outfitName, garmentIds: selectedIds })}
          >
            <Plus size={16} />
            {saveMutation.isPending ? 'Saving' : 'Save outfit'}
          </button>
          <BodyReferenceManager
            photos={bodyPhotos}
            selectedPhoto={selectedBodyPhoto}
            isLoading={bodyPhotosQuery.isLoading}
            deletingId={deleteBodyPhotoMutation.isPending ? deleteBodyPhotoMutation.variables : undefined}
            onSelect={setSelectedBodyPhotoId}
            onDelete={(id) => deleteBodyPhotoMutation.mutate(id)}
            onUpload={handleBodyPhotoFileChange}
          />
          <button
            type="button"
            className={sequentialFlowEnabled ? 'flow-toggle active' : 'flow-toggle'}
            aria-pressed={sequentialFlowEnabled}
            onClick={() => setSequentialFlowEnabled((enabled) => !enabled)}
          >
            <GitBranch size={16} />
            <span>Sequential flow</span>
            <strong style={headingStyle}>{sequentialFlowEnabled ? 'On' : 'Off'}</strong>
          </button>
          <button
            type="button"
            className="clay-button primary-action generate-action"
            disabled={selectedIds.length === 0 || !selectedBodyPhoto?.imageUrl || tryOnMutation.isPending}
            onClick={async () => {
              const outfit = await ensureOutfit();
              await tryOnMutation.mutateAsync({
                outfitId: outfit.id,
                bodyReferencePhotoUrl: selectedBodyPhoto.imageUrl,
                bodyReferencePhotoId: selectedBodyPhoto.id,
                consentAccepted: true,
                sequentialFlowEnabled
              });
              setMode('person');
            }}
          >
            <Sparkles size={16} />
            {tryOnMutation.isPending ? 'Generating' : 'Generate preview'}
          </button>
          {latestTryOnJob ? (
            <div className="tryon-status">
              <ScanFace size={17} />
              <div>
                <small style={headingStyle}>Try-on job</small>
                <strong style={headingStyle}>{latestTryOnJob.status}</strong>
              </div>
            </div>
          ) : null}
          <button
            type="button"
            className="clay-button secondary-action"
            disabled={!activeOutfit || shareMutation.isPending}
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
          {[quickAddGarmentError ? new Error(quickAddGarmentError) : null, bodyPhotoUploadError ? new Error(bodyPhotoUploadError) : null, saveMutation.error, tryOnMutation.error, shareMutation.error, deleteBodyPhotoMutation.error, tryOnJobQuery.error].filter(Boolean).map((error) => (
            <p className="error" key={(error as Error).message}>
              {(error as Error).message}
            </p>
          ))}
          <OutfitList outfits={outfitsQuery.data ?? []} onPick={setActiveOutfit} />
        </div>
      </aside>
    </section>
  );
}
