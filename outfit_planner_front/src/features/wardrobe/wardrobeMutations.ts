import { useMutation, useQueryClient } from '@tanstack/react-query';
import {
  accountEntitlementsQueryKey,
  createGarment,
  deleteGarment,
  updateGarment,
  uploadGarmentOriginal,
  type UpdateGarmentInput
} from '../../api/client';
import { garmentPhotoUrlsFromUpload } from '../uploads/uploadedPhotoUrls';
import type { GarmentItem } from '../../types';
import { isCreatableItem, type UploadQueueItem } from './wardrobeUpload';

export { garmentPhotoUrlsFromUpload } from '../uploads/uploadedPhotoUrls';

export const wardrobeQueryKey = ['garments'] as const;

// Thrown when a batch stops part-way (typically the plan's garment cap): callers learn which
// queue items were already created so they can drop them instead of re-creating duplicates.
export class UploadBatchError extends Error {
  readonly createdItemIds: string[];

  constructor(message: string, createdItemIds: string[], cause?: unknown) {
    super(message, { cause });
    this.name = 'UploadBatchError';
    this.createdItemIds = createdItemIds;
  }
}

export function useWardrobeMutations() {
  const queryClient = useQueryClient();
  const invalidateWardrobe = () => {
    void queryClient.invalidateQueries({ queryKey: wardrobeQueryKey });
    // Plan usage (garment count against the cap) changes with every create/delete.
    void queryClient.invalidateQueries({ queryKey: accountEntitlementsQueryKey });
  };

  const editMutation = useMutation({
    mutationFn: ({ garmentId, input }: { garmentId: string; input: UpdateGarmentInput }) => updateGarment(garmentId, input),
    onSuccess: invalidateWardrobe
  });

  const deleteMutation = useMutation({
    mutationFn: deleteGarment,
    onSuccess: invalidateWardrobe
  });

  const uploadQueueMutation = useMutation({
    mutationFn: async (items: UploadQueueItem[]) => {
      const creatableItems = items.filter(isCreatableItem);
      const created: GarmentItem[] = [];
      const createdItemIds: string[] = [];

      for (const item of creatableItems) {
        try {
          // The original was uploaded fast on selection (no rembg). Failed items get a final
          // upload attempt here. Background removal then runs asynchronously on the server, so the
          // garment is created immediately from the original with backgroundRemovalPending.
          const uploadedPhoto = item.uploadedPhoto ?? (await uploadGarmentOriginal(item.file));
          const photoUrls = garmentPhotoUrlsFromUpload(uploadedPhoto);
          created.push(await createGarment({
            name: item.name,
            category: item.category,
            imageUrl: photoUrls.imageUrl,
            thumbnailUrl: photoUrls.thumbnailUrl,
            tags: item.tags,
            primaryColor: item.primaryColor.trim() || null,
            season: item.season,
            perceptualHash: uploadedPhoto.perceptualHash ?? null,
            backgroundRemovalPending: true,
            // Null on this fast path (no cutout yet); the async background-removal worker
            // measures and persists the cutout once it exists.
            cutoutWidthPx: uploadedPhoto.cutoutWidthPx ?? null,
            cutoutHeightPx: uploadedPhoto.cutoutHeightPx ?? null
          }));
          createdItemIds.push(item.id);
        } catch (error) {
          const message = error instanceof Error ? error.message : String(error);
          const remaining = creatableItems.length - createdItemIds.length;
          throw new UploadBatchError(
            createdItemIds.length > 0
              ? `${createdItemIds.length} garment(s) were added; ${remaining} could not be: ${message}`
              : message,
            createdItemIds,
            error
          );
        }
      }

      return created;
    },
    // Settled, not success: a batch that stops part-way still created garments.
    onSettled: invalidateWardrobe
  });

  return {
    editMutation,
    deleteMutation,
    uploadQueueMutation
  };
}
