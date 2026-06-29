import { useMutation, useQueryClient } from '@tanstack/react-query';
import {
  createGarment,
  deleteGarment,
  updateGarment,
  uploadGarmentPhoto,
  type UpdateGarmentInput
} from '../../api/client';
import { garmentPhotoUrlsFromUpload } from '../uploads/uploadedPhotoUrls';
import type { GarmentItem } from '../../types';
import { duplicateGarmentInput } from './wardrobeFilters';
import type { UploadQueueItem } from './wardrobeUpload';

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
      const creatableItems = items.filter((item) => item.status === 'processed' || item.status === 'failed');
      const created: GarmentItem[] = [];

      for (const item of creatableItems) {
        // Processed items already ran background removal on selection; only failed
        // items (uploadedPhoto === null) need a final upload attempt here.
        const uploadedPhoto = item.uploadedPhoto ?? (await uploadGarmentPhoto(item.file));
        const photoUrls = garmentPhotoUrlsFromUpload(uploadedPhoto);
        created.push(await createGarment({
          name: item.name,
          category: item.category,
          imageUrl: photoUrls.imageUrl,
          thumbnailUrl: photoUrls.thumbnailUrl,
          tags: item.tags,
          primaryColor: item.primaryColor.trim() || null,
          season: item.season
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
