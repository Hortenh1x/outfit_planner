import type { GarmentCategory } from '../../types';

export function garmentNameFromFile(file: File, category: GarmentCategory) {
  const name = file.name
    .replace(/\.[^.]+$/, '')
    .replace(/[-_]+/g, ' ')
    .trim();

  return name || category;
}
