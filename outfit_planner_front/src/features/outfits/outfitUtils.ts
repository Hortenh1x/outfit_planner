import type { GarmentCategory, GarmentItem, OutfitSelection } from '../../types';

export const GARMENT_CATEGORIES: GarmentCategory[] = ['Top', 'Bottom', 'Dress', 'Outerwear', 'Shoes', 'Bag', 'Accessory'];

export const CATEGORY_SELECTION_KEYS: Record<GarmentCategory, keyof OutfitSelection> = {
  Top: 'topId',
  Bottom: 'bottomId',
  Dress: 'dressId',
  Outerwear: 'outerwearId',
  Shoes: 'shoesId',
  Bag: 'bagId',
  Accessory: 'accessoryId'
};

export function groupGarmentsByCategory(garments: GarmentItem[]): Record<GarmentCategory, GarmentItem[]> {
  return GARMENT_CATEGORIES.reduce((grouped, category) => {
    grouped[category] = garments.filter((garment) => garment.category === category);
    return grouped;
  }, {} as Record<GarmentCategory, GarmentItem[]>);
}

export function selectedGarmentIds(selection: OutfitSelection): string[] {
  return GARMENT_CATEGORIES
    .map((category) => selection[CATEGORY_SELECTION_KEYS[category]])
    .filter((id): id is string => Boolean(id));
}

export function selectionLabel(selection: OutfitSelection, garments: GarmentItem[]): string {
  const selected = selectedGarmentIds(selection)
    .map((id) => garments.find((garment) => garment.id === id)?.name)
    .filter((name): name is string => Boolean(name));

  if (selected.length === 0) {
    return 'Choose outfit pieces';
  }

  if (selected.length === 1) {
    return `${selected[0]} + choose another piece`;
  }

  return selected.join(' + ');
}
