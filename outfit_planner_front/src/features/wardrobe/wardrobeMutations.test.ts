import { describe, expect, it } from 'vitest';
import { garmentPhotoUrlsFromUpload } from './wardrobeMutations';
import type { UploadedPhotoResponse } from '../../api/client';

describe('wardrobe mutations', () => {
  it('uses the processed cutout as the garment image when upload variants are available', () => {
    const uploaded: UploadedPhotoResponse = {
      fileName: 'shirt.png',
      contentType: 'image/png',
      length: 123,
      url: '/api/storage/signed/garments/processed-cutout/shirt.png',
      originalUrl: '/api/storage/signed/garments/original/shirt.png',
      thumbnailUrl: '/api/storage/signed/garments/thumbnail/shirt.png',
      cutoutUrl: '/api/storage/signed/garments/processed-cutout/shirt.png',
      maskUrl: '/api/storage/signed/garments/segmentation-mask/shirt.png'
    };

    expect(garmentPhotoUrlsFromUpload(uploaded)).toEqual({
      imageUrl: '/api/storage/signed/garments/processed-cutout/shirt.png',
      thumbnailUrl: '/api/storage/signed/garments/processed-cutout/shirt.png'
    });
  });

  it('falls back to the legacy upload URL when variant URLs are missing', () => {
    expect(garmentPhotoUrlsFromUpload({
      fileName: 'shirt.png',
      contentType: 'image/png',
      length: 123,
      url: '/api/storage/signed/garments/original/shirt.png'
    })).toEqual({
      imageUrl: '/api/storage/signed/garments/original/shirt.png',
      thumbnailUrl: '/api/storage/signed/garments/original/shirt.png'
    });
  });
});
