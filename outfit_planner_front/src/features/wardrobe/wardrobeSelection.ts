import type { GarmentCategory, GarmentItem } from '../../types';

/**
 * Quick-build selection for the Wardrobe tab: at most one garment per category. Modelled as a
 * category→garmentId map so "one per category" is structural — selecting another item in the same
 * category simply overwrites the slot. All transitions return a new object so the rules stay pure.
 */
export type WardrobeBuildSelection = Partial<Record<GarmentCategory, string>>;

/** Selects the garment in its category, or clears the slot when the same garment is toggled again. */
export function toggleWardrobeSelection(
  selection: WardrobeBuildSelection,
  garment: GarmentItem
): WardrobeBuildSelection {
  if (selection[garment.category] === garment.id) {
    const next = { ...selection };
    delete next[garment.category];
    return next;
  }

  return { ...selection, [garment.category]: garment.id };
}

export function isGarmentSelected(selection: WardrobeBuildSelection, garment: GarmentItem): boolean {
  return selection[garment.category] === garment.id;
}

export function wardrobeSelectionCount(selection: WardrobeBuildSelection): number {
  return Object.keys(selection).length;
}
