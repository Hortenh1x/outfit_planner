import { useMutation, useQueryClient } from '@tanstack/react-query';
import {
  createGarment,
  deleteGarment,
  updateGarment,
  uploadGarmentOriginal,
  type UpdateGarmentInput
} from '../../api/client';
import { garmentPhotoUrlsFromUpload } from '../uploads/uploadedPhotoUrls';
import type { GarmentItem } from '../../types';
import { duplicateGarmentInput } from './wardrobeFilters';
import { isCreatableItem, type UploadQueueItem } from './wardrobeUpload';

export { garmentPhotoUrlsFromUpload } from '../uploads/uploadedPhotoUrls';

export const wardrobeQueryKey = ['garments'] as const;

export function useWardrobeMutations() {
  const queryClient = useQueryClient();
  const invalidateWardrobe = () => {
    void queryClient.invalidateQueries({ queryKey: wardrobeQueryKey });
  };

  const favoriteMutation = useMutation({
    mutationFn: (garment: GarmentItem) => updateGarment(garment.id, { isFavorite: !garment.isFavorite }),
    onSuccess: invalidateWardrobe
  });

  const editMutation = useMutation({
    mutationFn: ({ garmentId, input }: { garmentId: string; input: UpdateGarmentInput }) => updateGarment(garmentId, input),
    onSuccess: invalidateWardrobe
  });

  const duplicateMutation = useMutation({
    mutationFn: (garment: GarmentItem) => createGarment(duplicateGarmentInput(garment)),
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

      for (const item of creatableItems) {
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
          backgroundRemovalPending: true
        }));
      }

      return created;
    },
    onSuccess: invalidateWardrobe
  });

  return {
    favoriteMutation,
    editMutation,
    duplicateMutation,
    deleteMutation,
    uploadQueueMutation
  };
}
