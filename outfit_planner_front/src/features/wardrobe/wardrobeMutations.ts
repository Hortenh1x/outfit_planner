import { useMutation, useQueryClient } from '@tanstack/react-query';
import {
  createGarment,
  deleteGarment,
  updateGarment,
  uploadGarmentPhoto,
  type UploadedPhotoResponse,
  type UpdateGarmentInput
} from '../../api/client';
import type { GarmentItem } from '../../types';
import { duplicateGarmentInput } from './wardrobeFilters';
import type { UploadQueueItem } from './wardrobeUpload';

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

  const archiveMutation = useMutation({
    mutationFn: (garment: GarmentItem) => updateGarment(garment.id, { isArchived: !garment.isArchived }),
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
      const validItems = items.filter((item) => !item.validationError && (item.status === 'ready' || item.status === 'failed'));
      const created: GarmentItem[] = [];

      for (const item of validItems) {
        const uploadedPhoto = await uploadGarmentPhoto(item.file);
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
    archiveMutation,
    editMutation,
    duplicateMutation,
    deleteMutation,
    uploadQueueMutation
  };
}

export function garmentPhotoUrlsFromUpload(uploadedPhoto: UploadedPhotoResponse) {
  const imageUrl = uploadedPhoto.cutoutUrl || uploadedPhoto.url;
  return {
    imageUrl,
    thumbnailUrl: uploadedPhoto.thumbnailUrl || imageUrl
  };
}
