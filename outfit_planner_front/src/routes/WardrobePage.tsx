import { useMemo, useState } from 'react';
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
import {
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
  const [uploadDefaults, setUploadDefaults] = useState<WardrobeUploadDefaults>(defaultUploadDefaults);
  const [uploadQueue, setUploadQueue] = useState<UploadQueueItem[]>([]);
  const apiFilters = useMemo(() => toGarmentFilters(filters), [filters]);
  const garmentsQuery = useQuery({
    queryKey: [...wardrobeQueryKey, apiFilters],
    queryFn: () => listGarments(apiFilters)
  });
  const mutations = useWardrobeMutations();
  const allGarments = garmentsQuery.data ?? [];
  const garments = filterGarmentsByLocalTags(allGarments, filters.tag);
  const existingTags = useMemo(
    () => Array.from(new Set([...uploadDefaults.tags, ...allGarments.flatMap((garment) => garment.tags)])).slice(0, 8),
    [allGarments, uploadDefaults.tags]
  );

  function addFiles(files: File[]) {
    if (files.length === 0) {
      return;
    }

    setEditingGarment(null);
    setUploadQueue((current) => [
      ...current,
      ...createUploadQueueItems(files, {
        category: uploadDefaults.category,
        color: uploadDefaults.color,
        season: uploadDefaults.season,
        existingTags
      })
    ]);
  }

  function changeQueueItem(itemId: string, updates: UploadQueueItemUpdates) {
    setUploadQueue((current) => current.map((item) => (item.id === itemId ? updateUploadQueueItem(item, updates) : item)));
  }

  function acceptSuggestedTag(itemId: string, tag: string) {
    setUploadQueue((current) => current.map((item) => {
      if (item.id !== itemId || item.tags.includes(tag)) {
        return item;
      }

      return updateUploadQueueItem(item, { tags: [...item.tags, tag] });
    }));
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
                onArchive={(item) => mutations.archiveMutation.mutate(item)}
                onDelete={(item) => mutations.deleteMutation.mutate(item.id)}
                onDuplicate={(item) => mutations.duplicateMutation.mutate(item)}
                onEdit={setEditingGarment}
                onFavorite={(item) => mutations.favoriteMutation.mutate(item)}
              />
            ))}
          </div>
        )}
        {[
          garmentsQuery.error,
          mutations.favoriteMutation.error,
          mutations.archiveMutation.error,
          mutations.editMutation.error,
          mutations.duplicateMutation.error,
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
          defaults={uploadDefaults}
          onAcceptTag={acceptSuggestedTag}
          onAddFiles={addFiles}
          onChangeItem={changeQueueItem}
          onDefaultsChange={setUploadDefaults}
          onRemoveItem={(itemId) => setUploadQueue((current) => current.filter((item) => item.id !== itemId))}
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
  if (mutations.favoriteMutation.isPending && mutations.favoriteMutation.variables?.id === garment.id) {
    return 'favorite';
  }
  if (mutations.archiveMutation.isPending && mutations.archiveMutation.variables?.id === garment.id) {
    return 'archive';
  }
  if (mutations.duplicateMutation.isPending && mutations.duplicateMutation.variables?.id === garment.id) {
    return 'duplicate';
  }
  if (mutations.deleteMutation.isPending && mutations.deleteMutation.variables === garment.id) {
    return 'delete';
  }
  return undefined;
}

function needsBetterPhoto(garment: GarmentItem): boolean {
  const imageName = `${garment.imageUrl} ${garment.thumbnailUrl ?? ''}`.toLowerCase();
  return !garment.thumbnailUrl || /\b(img|image|photo|dsc|pxl)[_-]?\d+\b/.test(imageName);
}
