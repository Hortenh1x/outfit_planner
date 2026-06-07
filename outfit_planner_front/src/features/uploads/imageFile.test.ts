import { describe, expect, it } from 'vitest';
import { isSupportedImageFile, readImageFileAsDataUrl, validateUploadImageFile } from './imageFile';

describe('image file helpers', () => {
  it('accepts common image file types and rejects non-images', () => {
    expect(isSupportedImageFile(new File(['image'], 'shirt.png', { type: 'image/png' }))).toBe(true);
    expect(isSupportedImageFile(new File(['image'], 'shirt.webp', { type: 'image/webp' }))).toBe(true);
    expect(isSupportedImageFile(new File(['text'], 'notes.txt', { type: 'text/plain' }))).toBe(false);
  });

  it('reads an image file as a data URL', async () => {
    const file = new File(['shirt'], 'shirt.png', { type: 'image/png' });

    await expect(readImageFileAsDataUrl(file)).resolves.toMatch(/^data:image\/png;base64,/);
  });

  it('rejects files larger than the upload limit before sending them over the network', () => {
    const file = new File(['image'], 'large.png', { type: 'image/png' });
    Object.defineProperty(file, 'size', { value: 51 * 1024 * 1024 });

    expect(() => validateUploadImageFile(file)).toThrow(/50 MB or smaller/i);
  });
});
