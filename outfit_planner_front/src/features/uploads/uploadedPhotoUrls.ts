import type { UploadedPhotoResponse } from '../../api/client';

export function garmentPhotoUrlsFromUpload(uploadedPhoto: UploadedPhotoResponse) {
  const imageUrl = uploadedPhoto.cutoutUrl || uploadedPhoto.url;
  return {
    imageUrl,
    thumbnailUrl: uploadedPhoto.cutoutUrl || uploadedPhoto.thumbnailUrl || imageUrl
  };
}
