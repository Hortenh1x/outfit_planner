const supportedImageTypes = new Set(['image/jpeg', 'image/png', 'image/webp']);
export const maxUploadImageBytes = 50 * 1024 * 1024;

export function isSupportedImageFile(file: File): boolean {
  return supportedImageTypes.has(file.type);
}

export function validateUploadImageFile(file: File): void {
  if (!isSupportedImageFile(file)) {
    throw new Error('Upload a JPG, PNG, or WebP image.');
  }

  if (file.size > maxUploadImageBytes) {
    throw new Error('Photo file must be 50 MB or smaller.');
  }
}

export function readImageFileAsDataUrl(file: File): Promise<string> {
  try {
    validateUploadImageFile(file);
  } catch (error) {
    return Promise.reject(error);
  }

  return new Promise((resolve, reject) => {
    const reader = new FileReader();

    reader.onload = () => {
      if (typeof reader.result === 'string') {
        resolve(reader.result);
        return;
      }

      reject(new Error('Could not read the selected image.'));
    };
    reader.onerror = () => reject(new Error('Could not read the selected image.'));
    reader.readAsDataURL(file);
  });
}
