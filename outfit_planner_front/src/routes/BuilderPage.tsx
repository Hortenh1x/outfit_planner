import { type ChangeEvent, useState } from 'react';
import { useEffect } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Coins, GitBranch, Layers3, Link2, Plus, ScanFace, Sparkles, Trash2, Wand2 } from 'lucide-react';
import { accountEntitlementsQueryKey, createBodyReferencePhoto, createGarment, createOutfit, deleteBodyReferencePhoto, deleteOutfit, deleteOutfitTryOnPreview, deleteTryOnJobOutput, estimateTryOn, getAccountEntitlements, getTryOnJob, listBodyReferencePhotos, listGarments, listOutfits, shareOutfit, startTryOn, updateOutfit, uploadBodyReferencePhoto, uploadGarmentPhoto } from '../api/client';
import { ModeToggle } from '../components/ModeToggle';
import { BodyReferenceManager } from '../features/builder/BodyReferenceManager';
import { garmentNameFromFile } from '../features/builder/garmentName';
import { OutfitList } from '../features/builder/OutfitList';
import { SlotPicker } from '../features/builder/SlotPicker';
import { useAuthSession } from '../features/auth/authQueries';
import { groupGarmentsByCategory } from '../features/outfits/outfitUtils';
import {
  EMPTY_COMPOSED_SELECTION,
  composedSelectionFromCategoryMap,
  composedSelectionFromOutfit,
  cycleCarousel,
  cycleDress,
  deriveGarmentIds,
  ensureComposedDefaults,
  toggleAccessory,
  toggleBag,
  toggleDress,
  toggleOuterwear,
  unselectDress,
  type ComposedSelection
} from '../features/outfits/composedOutfit';
import { ComposedOutfitFigure, defaultFigureWidth, type ComposedFigureGarment } from '../features/outfits/ComposedOutfitFigure';
import { creditsLabel, modeLabel } from '../features/tryon/tryOnText';
import { garmentPhotoUrlsFromUpload } from '../features/uploads/uploadedPhotoUrls';
import { validateUploadImageFile } from '../features/uploads/imageFile';
import { EmptyPreview } from '../shared/ui/EmptyPreview';
import { PanelTitle } from '../shared/ui/PanelTitle';
import { PanelSkeleton } from '../shared/ui/Skeletons';
import type { GarmentCategory, GarmentItem, Outfit, PreviewMode, TryOnCostEstimate, TryOnMode } from '../types';

const tryOnJobPollIntervalMs = 1000;

// Every wardrobe category is pickable from the list. Top, bottom, and shoes are additionally
// cyclable directly on the figure (swipe on touch, arrows on desktop) and keep their
// first-item-worn-by-default behaviour; clicking their list entry sets the worn piece. The
// hairstyle stays hidden from the product.
const LIST_CATEGORIES: GarmentCategory[] = ['Top', 'Bottom', 'Dress', 'Outerwear', 'Shoes', 'Bag', 'Accessory'];

export function BuilderPage() {
  const queryClient = useQueryClient();
  const location = useLocation();
  const navigate = useNavigate();
  const garmentsQuery = useQuery({ queryKey: ['garments'], queryFn: listGarments });
  const outfitsQuery = useQuery({ queryKey: ['outfits'], queryFn: listOutfits });
  const bodyPhotosQuery = useQuery({ queryKey: ['body-reference-photos'], queryFn: listBodyReferencePhotos });
  const sessionQuery = useAuthSession();
  const profileGender = sessionQuery.data?.user?.gender ?? null;
  const garments = garmentsQuery.data ?? [];
  const grouped = groupGarmentsByCategory(garments);
  // Hairstyles are currently hidden from the product; the figure and rules keep the capability
  // for later, but the Builder composes with no hairstyle list.
  const hairstyles: never[] = [];
  const [composed, setComposed] = useState<ComposedSelection>(EMPTY_COMPOSED_SELECTION);
  const [mode, setMode] = useState<PreviewMode>('clothes');
  const [outfitName, setOutfitName] = useState('Today');
  const [selectedBodyPhotoId, setSelectedBodyPhotoId] = useState('');
  const [bodyPhotoUploadError, setBodyPhotoUploadError] = useState<string | null>(null);
  const [quickAddGarmentError, setQuickAddGarmentError] = useState<string | null>(null);
  const [tryOnMode, setTryOnMode] = useState<TryOnMode>('SequentialOutfitTryOn');
  const [pendingEstimate, setPendingEstimate] = useState<TryOnCostEstimate | null>(null);
  const [activeOutfit, setActiveOutfit] = useState<Outfit | null>(null);

  // A quick-build selection handed over from the Wardrobe tab (one garment per category). Applied
  // once, then the router state is cleared so a refresh or back-navigation does not reapply it.
  useEffect(() => {
    const handed = (location.state as { wardrobeCompose?: Partial<Record<GarmentCategory, string>> } | null)?.wardrobeCompose;
    if (!handed) {
      return;
    }

    setComposed(composedSelectionFromCategoryMap(handed));
    setActiveOutfit(null);
    setMode('clothes');
    navigate(location.pathname, { replace: true, state: null });
  }, [location, navigate]);

  // The stored state keeps only explicit choices; defaults (first item of each on-figure
  // category, first hairstyle) are layered on at render time so they follow wardrobe changes.
  const effectiveComposed = ensureComposedDefaults(composed, grouped, hairstyles);
  const selectedIds = deriveGarmentIds(effectiveComposed);
  const activeOutfitHasChanges = activeOutfit
    ? outfitName !== activeOutfit.name
      || !sameIds(selectedIds, activeOutfit.items.map((item) => item.garmentId))
    : false;

  const saveMutation = useMutation({
    mutationFn: () => {
      const input = {
        name: outfitName,
        garmentIds: selectedIds,
        silhouetteGender: profileGender
      };
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
  const entitlementsQuery = useQuery({ queryKey: accountEntitlementsQueryKey, queryFn: getAccountEntitlements, retry: 1 });
  // A cache hit never debits credits, so a zero balance may still confirm a cached run.
  const insufficientCredits = pendingEstimate != null
    && pendingEstimate.requiresAi
    && !pendingEstimate.hasCachedResult
    && pendingEstimate.creditsUnlimited !== true
    && typeof pendingEstimate.creditBalance === 'number'
    && pendingEstimate.creditBalance < pendingEstimate.estimatedCredits;
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
        tags: [],
        cutoutWidthPx: uploadedPhoto.cutoutWidthPx ?? null,
        cutoutHeightPx: uploadedPhoto.cutoutHeightPx ?? null
      });
    },
    onSuccess: (garment) => {
      applyComposedChange((current) => placeGarment(current, garment));
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
    if (latestTryOnJob?.status !== 'Succeeded') {
      return;
    }

    void queryClient.invalidateQueries({ queryKey: ['outfits'] });

    // The generated preview is saved onto the outfit by the backend; mirror it onto the active
    // outfit so it is reflected immediately (the saved-outfit card shows it, and a later metadata
    // save keeps it). Free clothes-only jobs have no output url, so nothing is set for them.
    const outputImageUrl = latestTryOnJob.outputImageUrl;
    if (outputImageUrl) {
      setActiveOutfit((current) =>
        current && current.personPreviewUrl !== outputImageUrl
          ? { ...current, personPreviewUrl: outputImageUrl }
          : current
      );
    }
  }, [latestTryOnJob?.status, latestTryOnJob?.outputImageUrl, queryClient]);

  async function ensureOutfit() {
    if (activeOutfit && !activeOutfitHasChanges) {
      return activeOutfit;
    }

    return await saveMutation.mutateAsync();
  }

  // Any composition change invalidates a pending estimate/preview, mirroring the old slot flow.
  function applyComposedChange(transform: (current: ComposedSelection) => ComposedSelection) {
    setComposed(transform(effectiveComposed));
    setPendingEstimate(null);
    estimateMutation.reset();
    tryOnMutation.reset();
    shareMutation.reset();
  }

  function placeGarment(selection: ComposedSelection, garment: GarmentItem): ComposedSelection {
    switch (garment.category) {
      case 'Top':
        return { ...selection, dressId: undefined, topId: garment.id };
      case 'Bottom':
        return { ...selection, dressId: undefined, bottomId: garment.id };
      case 'Shoes':
        return { ...selection, shoesId: garment.id };
      case 'Dress':
        return toggleDress(selection, garment.id);
      case 'Outerwear':
        return toggleOuterwear(selection, garment.id);
      case 'Bag':
        return toggleBag(selection, garment.id);
      case 'Accessory':
        return toggleAccessory(selection, garment.id);
      default:
        return selection;
    }
  }

  // List clicks place the garment exactly like quick-add: top/bottom/shoes set the worn piece
  // (dropping any worn dress), while dress/outerwear/bag/accessory toggle. Reusing placeGarment
  // keeps the list picks and the on-figure interactions in sync.
  function handleListSelect(id: string) {
    const garment = garments.find((item) => item.id === id);
    if (!garment) {
      return;
    }

    applyComposedChange((current) => placeGarment(current, garment));
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

  function handlePickOutfit(outfit: Outfit) {
    setActiveOutfit(outfit);
    setComposed(composedSelectionFromOutfit(outfit));
    setOutfitName(outfit.name);
    setPendingEstimate(null);
    estimateMutation.reset();
    tryOnMutation.reset();
    setMode(outfit.personPreviewUrl ? 'person' : 'clothes');
  }

  const figureGender = profileGender ?? 'Female';
  const lookup = (id?: string) => garments.find((garment) => garment.id === id);
  const figureTop = toFigureGarment(lookup(effectiveComposed.topId));
  const figureBottom = toFigureGarment(lookup(effectiveComposed.bottomId));
  const figureDress = toFigureGarment(lookup(effectiveComposed.dressId));
  const figureShoes = toFigureGarment(lookup(effectiveComposed.shoesId));
  const figureOuterwear = toFigureGarment(lookup(effectiveComposed.outerwearId));
  const figureBag = toFigureGarment(lookup(effectiveComposed.bagId));
  const figureAccessories = effectiveComposed.accessoryIds
    .map((id) => toFigureGarment(lookup(id)))
    .filter((garment): garment is ComposedFigureGarment => garment !== null);

  return (
    <section className="builder-editorial-page">
      <aside className="builder-wardrobe-rail">
        <PanelTitle icon={<Layers3 size={19} />} title="Wardrobe pieces" />
        {garmentsQuery.isLoading ? (
          <PanelSkeleton />
        ) : (
          <>
            {LIST_CATEGORIES.map((category) => (
              <SlotPicker
                key={category}
                title={category}
                category={category}
                garments={grouped[category]}
                selectedId={selectedIdForCategory(category, effectiveComposed)}
                selectedIds={category === 'Accessory' ? effectiveComposed.accessoryIds : undefined}
                onSelect={handleListSelect}
                onQuickAdd={(event) => handleQuickAddGarment(category, event)}
                isQuickAdding={quickAddGarmentMutation.isPending && quickAddGarmentMutation.variables?.category === category}
              />
            ))}
          </>
        )}
      </aside>

      <div className="preview-stage">
        <header className="builder-hero">
          <div>
            <h1>Builder</h1>
          </div>
          <span>{composedLabel(selectedIds, garments)}</span>
          <ModeToggle mode={mode} onChange={setMode} />
        </header>

        <div className="preview-canvas builder-preview">
          {mode === 'clothes' ? (
            <div className="composed-stage">
              <ComposedOutfitFigure
                gender={figureGender}
                top={figureTop}
                bottom={figureBottom}
                dress={figureDress}
                shoes={figureShoes}
                outerwear={figureOuterwear}
                bag={figureBag}
                accessories={figureAccessories}
                width={defaultFigureWidth()}
                interactive={{
                  cycleAvailability: {
                    top: !effectiveComposed.dressId && grouped.Top.length > 1,
                    bottom: !effectiveComposed.dressId && grouped.Bottom.length > 1,
                    shoes: grouped.Shoes.length > 1,
                    dress: Boolean(effectiveComposed.dressId) && grouped.Dress.length > 1
                  },
                  emptyCarouselSlots: {
                    top: grouped.Top.length === 0,
                    bottom: grouped.Bottom.length === 0,
                    shoes: grouped.Shoes.length === 0
                  },
                  onCycle: (slot, direction) =>
                    applyComposedChange((current) => {
                      switch (slot) {
                        case 'top':
                          return cycleCarousel(current, 'Top', grouped.Top, direction);
                        case 'bottom':
                          return cycleCarousel(current, 'Bottom', grouped.Bottom, direction);
                        case 'shoes':
                          return cycleCarousel(current, 'Shoes', grouped.Shoes, direction);
                        case 'dress':
                          return cycleDress(current, grouped.Dress, direction);
                        default:
                          return current;
                      }
                    }),
                  onRemove: (slot) =>
                    applyComposedChange((current) => {
                      switch (slot) {
                        case 'dress':
                          return unselectDress(current);
                        case 'outerwear':
                          return current.outerwearId ? toggleOuterwear(current, current.outerwearId) : current;
                        case 'bag':
                          return current.bagId ? toggleBag(current, current.bagId) : current;
                        default:
                          return current;
                      }
                    }),
                  onRemoveAccessory: (garmentId) => applyComposedChange((current) => toggleAccessory(current, garmentId))
                }}
              />
              {sessionQuery.data?.user && !profileGender ? (
                <p className="composed-figure-hint">
                  Set your gender in account settings so the silhouette matches you.
                </p>
              ) : null}
            </div>
          ) : (
            <div className="person-preview">
              {previewUrl ? (
                <img src={previewUrl} alt="Generated try-on preview" />
              ) : latestTryOnJob?.status === 'Failed' ? (
                <div className="status" role="alert">
                  <p>Try-on failed{latestTryOnJob.error ? `: ${latestTryOnJob.error}` : '.'}</p>
                  <p>Adjust the outfit or body reference and generate again.</p>
                </div>
              ) : latestTryOnJob?.status === 'Queued' || latestTryOnJob?.status === 'Processing' ? (
                <p className="status" role="status">Generating your try-on…</p>
              ) : (
                <EmptyPreview />
              )}
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
            {(['ClothesOnlyPreview', 'SingleGarmentTryOn', 'SequentialOutfitTryOn', 'ExperimentalCompositeTryOn'] as TryOnMode[]).map((option) => {
              // Plan-gated AI modes stay clickable: the estimate explains the gate and the
              // upgrade path instead of a silently disabled button.
              const planGated = option !== 'ClothesOnlyPreview'
                && entitlementsQuery.data?.allowedAiModes != null
                && !entitlementsQuery.data.allowedAiModes.includes(option);
              return (
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
                  {planGated ? <span className="mode-premium-pill">Premium</span> : null}
                </button>
              );
            })}
          </div>
          {entitlementsQuery.data?.creditsUnlimited === true || typeof entitlementsQuery.data?.creditBalance === 'number' ? (
            <p className="tryon-credits" role="status">
              <Coins size={14} aria-hidden="true" />
              <span>
                AI credits: {entitlementsQuery.data.creditsUnlimited ? 'unlimited' : entitlementsQuery.data.creditBalance}
              </span>
            </p>
          ) : null}
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
              {pendingEstimate.requiresUpgrade ? (
                <p className="upgrade-notice">
                  This mode is part of the <em>Premium</em> plan. <Link to="/upgrade">See Premium plans</Link>.
                </p>
              ) : null}
              {insufficientCredits ? (
                <p className="error">
                  Not enough AI credits: balance {pendingEstimate.creditBalance}, required {pendingEstimate.estimatedCredits}.
                </p>
              ) : null}
              <button
                type="button"
                className="primary-action"
                disabled={!pendingEstimate.isAvailable || insufficientCredits || (pendingEstimate.requiresAi && (requiresProfileGender || !selectedBodyPhoto?.imageUrl)) || tryOnMutation.isPending}
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
                  // The debit changed the balance shown in the credits chip.
                  void queryClient.invalidateQueries({ queryKey: accountEntitlementsQueryKey });
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

// The single worn garment id highlighted in each list. Accessories are multi-select and use
// the SlotPicker `selectedIds` prop instead, so they return undefined here.
function selectedIdForCategory(category: GarmentCategory, composed: ComposedSelection): string | undefined {
  switch (category) {
    case 'Top':
      return composed.topId;
    case 'Bottom':
      return composed.bottomId;
    case 'Dress':
      return composed.dressId;
    case 'Outerwear':
      return composed.outerwearId;
    case 'Shoes':
      return composed.shoesId;
    case 'Bag':
      return composed.bagId;
    default:
      return undefined;
  }
}

function toFigureGarment(garment: GarmentItem | undefined): ComposedFigureGarment | null {
  if (!garment) {
    return null;
  }

  return {
    id: garment.id,
    name: garment.name,
    category: garment.category,
    imageUrl: garment.thumbnailUrl,
    cutoutWidthPx: garment.cutoutWidthPx,
    cutoutHeightPx: garment.cutoutHeightPx
  };
}

function composedLabel(selectedIds: string[], garments: GarmentItem[]): string {
  const names = selectedIds
    .map((id) => garments.find((garment) => garment.id === id)?.name)
    .filter((name): name is string => Boolean(name));

  if (names.length === 0) {
    return 'Compose your figure';
  }

  if (names.length === 1) {
    return `${names[0]} + choose another piece`;
  }

  return names.join(' + ');
}

function sameIds(left: string[], right: string[]) {
  if (left.length !== right.length) {
    return false;
  }

  const sortedLeft = [...left].sort();
  const sortedRight = [...right].sort();
  return sortedLeft.every((id, index) => id === sortedRight[index]);
}
