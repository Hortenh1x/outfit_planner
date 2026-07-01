import { useEffect, useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { listGarments, type UpdateGarmentInput } from '../api/client';
import { GarmentCard } from '../features/wardrobe/GarmentCard';
import { GarmentEditor, type GarmentEditorSaveInput } from '../features/wardrobe/GarmentEditor';
import { WardrobeFilters, type WardrobeViewMode } from '../features/wardrobe/WardrobeFilterControls';
import { WardrobeUploadPanel, type WardrobeUploadDefaults } from '../features/wardrobe/WardrobeUploadPanel';
import {
  defaultWardrobeFilters,
  filterGarmentsByLocalTags,
  toGarmentFilters,
  type WardrobeFilterState
} from '../features/wardrobe/wardrobeFilters';
import { useWardrobeMutations, wardrobeQueryKey } from '../features/wardrobe/wardrobeMutations';
import { useEagerGarmentUploads } from '../features/wardrobe/useEagerGarmentUploads';
import {
  computeDuplicateFlags,
  createUploadQueueItems,
  updateUploadQueueItem,
  type UploadQueueItem,
  type UploadQueueItemUpdates
} from '../features/wardrobe/wardrobeUpload';
import type { GarmentItem } from '../types';
import '../features/wardrobe/wardrobe.css';

const defaultUploadDefaults: WardrobeUploadDefaults = {
  category: 'Top',
  color: '',
  season: [],
  tags: []
};

export function WardrobePage() {
  const [filters, setFilters] = useState<WardrobeFilterState>(defaultWardrobeFilters);
  const [viewMode, setViewMode] = useState<WardrobeViewMode>('grid');
  const [editingGarment, setEditingGarment] = useState<GarmentItem | null>(null);
  const [uploadQueue, setUploadQueue] = useState<UploadQueueItem[]>([]);
  const apiFilters = useMemo(() => toGarmentFilters(filters), [filters]);
  const garmentsQuery = useQuery({
    queryKey: [...wardrobeQueryKey, apiFilters],
    queryFn: () => listGarments(apiFilters),
    // Poll while any garment is still having its background removed, so the cutout appears
    // automatically once the async worker finishes.
    refetchInterval: (query) => {
      const garments = query.state.data ?? [];
      const removing = garments.some(
        (garment) => garment.backgroundRemovalStatus === 'Pending' || garment.backgroundRemovalStatus === 'Processing'
      );
      return removing ? 1500 : false;
    }
  });
  const mutations = useWardrobeMutations();
  const uploads = useEagerGarmentUploads(setUploadQueue);
  const allGarments = garmentsQuery.data ?? [];

  // Kick off background removal as soon as items land in the queue, and refill
  // concurrency slots whenever an upload settles and the queue changes.
  useEffect(() => {
    uploads.start(uploadQueue);
  }, [uploadQueue, uploads]);

  // Flag processed photos that duplicate an existing garment or another queued item (compared by
  // the pre-background-removal perceptual hash); flagged items are excluded from submit.
  useEffect(() => {
    const existingHashes = (garmentsQuery.data ?? [])
      .map((garment) => garment.perceptualHash)
      .filter((hash): hash is string => Boolean(hash));
    setUploadQueue((current) => {
      const flags = computeDuplicateFlags(current, existingHashes);
      let changed = false;
      const next = current.map((item) => {
        const duplicate = flags.get(item.id) ?? null;
        if ((item.duplicate ?? null) === duplicate) {
          return item;
        }

        changed = true;
        return { ...item, duplicate };
      });

      return changed ? next : current;
    });
  }, [uploadQueue, garmentsQuery.data]);
  const garments = filterGarmentsByLocalTags(allGarments, filters.tag);
  const existingTags = useMemo(() => {
    const tags = allGarments.flatMap((garment) => garment.tags ?? []);
    return Array.from(new Set(tags)).sort((left, right) => left.localeCompare(right));
  }, [allGarments]);

  function addFiles(files: File[]) {
    if (files.length === 0) {
      return;
    }

    setEditingGarment(null);
    setUploadQueue((current) => [
      ...current,
      ...createUploadQueueItems(files, {
        category: defaultUploadDefaults.category,
        color: defaultUploadDefaults.color,
        season: defaultUploadDefaults.season,
        existingTags
      })
    ]);
  }

  function changeQueueItem(itemId: string, updates: UploadQueueItemUpdates) {
    setUploadQueue((current) => current.map((item) => (item.id === itemId ? updateUploadQueueItem(item, updates) : item)));
  }

  function retryQueueItem(itemId: string) {
    // Re-queue the item; the eager-upload effect restarts processing.
    setUploadQueue((current) =>
      current.map((item) => (item.id === itemId ? { ...item, status: 'queued', error: null } : item))
    );
  }

  function removeQueueItem(itemId: string) {
    uploads.abort(itemId);
    setUploadQueue((current) => current.filter((item) => item.id !== itemId));
  }

  function resetFilters() {
    setFilters(defaultWardrobeFilters);
  }

  function saveEditedGarment(garmentId: string, input: GarmentEditorSaveInput) {
    const { imageUrl: _imageUrl, thumbnailUrl: _thumbnailUrl, ...metadataInput } = input;
    mutations.editMutation.mutate(
      { garmentId, input: metadataInput satisfies UpdateGarmentInput },
      { onSuccess: () => setEditingGarment(null) }
    );
  }

  const hasActiveFilters = JSON.stringify(filters) !== JSON.stringify(defaultWardrobeFilters);

  return (
    <section className="wardrobe-editorial-page">
      <div className="wardrobe-main">
        <header className="wardrobe-hero">
          <span>My wardrobe</span>
          <h1>Every piece has <em>a purpose.</em></h1>
        </header>
        <WardrobeFilters
          filters={filters}
          existingTags={existingTags}
          itemCount={garments.length}
          viewMode={viewMode}
          onChange={setFilters}
          onReset={resetFilters}
          onViewModeChange={setViewMode}
        />
        {garmentsQuery.isLoading ? (
          <div className="wardrobe-skeleton-grid" aria-label="Loading wardrobe">
            {Array.from({ length: 8 }).map((_, index) => <span key={index} />)}
          </div>
        ) : garments.length === 0 ? (
          <WardrobeEmptyState filtered={hasActiveFilters} onReset={resetFilters} />
        ) : (
          <div className={`wardrobe-catalog ${viewMode}`} aria-label="Wardrobe catalog">
            {garments.map((garment) => (
              <GarmentCard
                key={garment.id}
                garment={garment}
                needsBetterPhoto={needsBetterPhoto(garment)}
                pendingAction={pendingActionFor(garment, mutations)}
                onDelete={(item) => {
                  if (window.confirm(`Delete “${item.name}”? This cannot be undone.`)) {
                    mutations.deleteMutation.mutate(item.id);
                  }
                }}
                onEdit={setEditingGarment}
              />
            ))}
          </div>
        )}
        {[
          garmentsQuery.error,
          mutations.editMutation.error,
          mutations.deleteMutation.error,
          mutations.uploadQueueMutation.error
        ].filter(Boolean).map((error) => (
          <p className="wardrobe-error" key={(error as Error).message}>{(error as Error).message}</p>
        ))}
      </div>
      {editingGarment ? (
        <GarmentEditor
          garment={editingGarment}
          isSaving={mutations.editMutation.isPending}
          onCancel={() => setEditingGarment(null)}
          onSave={saveEditedGarment}
        />
      ) : (
        <WardrobeUploadPanel
          queue={uploadQueue}
          isUploading={mutations.uploadQueueMutation.isPending}
          onAddFiles={addFiles}
          onChangeItem={changeQueueItem}
          onRemoveItem={removeQueueItem}
          onRetryItem={retryQueueItem}
          onSubmitAll={() => mutations.uploadQueueMutation.mutate(uploadQueue, { onSuccess: () => setUploadQueue([]) })}
        />
      )}
    </section>
  );
}

function WardrobeEmptyState({ filtered, onReset }: { filtered: boolean; onReset: () => void }) {
  return (
    <section className="wardrobe-empty">
      <h2>{filtered ? 'No pieces match these filters' : 'Start with a front-view shirt, jeans, shoes, and one outer layer.'}</h2>
      <p>{filtered ? 'Reset filters to return to the full closet.' : 'A few clean photos are enough to make Builder and Calendar useful.'}</p>
      {filtered ? <button type="button" className="wardrobe-secondary-button" onClick={onReset}>Reset filters</button> : null}
    </section>
  );
}

function pendingActionFor(garment: GarmentItem, mutations: ReturnType<typeof useWardrobeMutations>): string | undefined {
  if (mutations.deleteMutation.isPending && mutations.deleteMutation.variables === garment.id) {
    return 'delete';
  }
  return undefined;
}

function needsBetterPhoto(garment: GarmentItem): boolean {
  const imageName = `${garment.imageUrl} ${garment.thumbnailUrl ?? ''}`.toLowerCase();
  return !garment.thumbnailUrl || /\b(img|image|photo|dsc|pxl)[_-]?\d+\b/.test(imageName);
}
