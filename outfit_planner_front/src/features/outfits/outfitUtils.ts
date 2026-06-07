import type { GarmentCategory, GarmentItem, OutfitSelection } from '../../types';

export function groupGarmentsByCategory(garments: GarmentItem[]): Record<GarmentCategory, GarmentItem[]> {
  return {
    Top: garments.filter((garment) => garment.category === 'Top'),
    Bottom: garments.filter((garment) => garment.category === 'Bottom')
  };
}

export function selectedGarmentIds(selection: OutfitSelection): string[] {
  return [selection.topId, selection.bottomId].filter((id): id is string => Boolean(id));
}

export function selectionLabel(selection: OutfitSelection, garments: GarmentItem[]): string {
  const top = garments.find((garment) => garment.id === selection.topId);
  const bottom = garments.find((garment) => garment.id === selection.bottomId);

  if (!top && !bottom) {
    return 'Choose a top and a bottom';
  }

  if (top && !bottom) {
    return `${top.name} + choose a bottom`;
  }

  if (!top && bottom) {
    return `Choose a top + ${bottom.name}`;
  }

  return `${top?.name} + ${bottom?.name}`;
}
