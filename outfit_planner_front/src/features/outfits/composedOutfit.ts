import type { GarmentCategory, GarmentItem, HairstylePreset, Outfit } from '../../types';

/**
 * Pure state logic for the composed-figure Builder: which garment occupies each silhouette
 * slot, which side pieces are placed, and the worn hairstyle. All transitions return a new
 * selection object so the rules stay unit-testable without any DOM.
 */
export interface ComposedSelection {
  topId?: string;
  bottomId?: string;
  shoesId?: string;
  dressId?: string;
  outerwearId?: string;
  bagId?: string;
  accessoryIds: string[];
  hairstyleId?: string;
  hairstyleVisible: boolean;
  /** Exact top/bottom worn before a dress was selected, restored verbatim on dress unselect. */
  rememberedTopId?: string;
  rememberedBottomId?: string;
}

export type CarouselCategory = 'Top' | 'Bottom' | 'Shoes';

export const MAX_ACCESSORIES = 3;

export const EMPTY_COMPOSED_SELECTION: ComposedSelection = {
  accessoryIds: [],
  hairstyleVisible: true
};

/** Fills the always-worn carousel slots (and the hairstyle) with each list's first entry. */
export function ensureComposedDefaults(
  selection: ComposedSelection,
  garmentsByCategory: Partial<Record<GarmentItem['category'], GarmentItem[]>>,
  hairstyles: HairstylePreset[]
): ComposedSelection {
  const next = { ...selection };
  if (!next.dressId) {
    next.topId = ensureListedId(next.topId, garmentsByCategory.Top);
    next.bottomId = ensureListedId(next.bottomId, garmentsByCategory.Bottom);
  }

  next.shoesId = ensureListedId(next.shoesId, garmentsByCategory.Shoes);
  if (!next.hairstyleId && hairstyles.length > 0) {
    next.hairstyleId = hairstyles[0].id;
  }

  return next;
}

function ensureListedId(currentId: string | undefined, garments: GarmentItem[] | undefined): string | undefined {
  if (!garments || garments.length === 0) {
    return undefined;
  }

  return currentId && garments.some((garment) => garment.id === currentId) ? currentId : garments[0].id;
}

/** Cycles an on-figure carousel slot through the category's garments, wrapping around. */
export function cycleCarousel(
  selection: ComposedSelection,
  category: CarouselCategory,
  garments: GarmentItem[],
  direction: 1 | -1
): ComposedSelection {
  if (garments.length === 0) {
    return selection;
  }

  if (selection.dressId && (category === 'Top' || category === 'Bottom')) {
    // Tops and bottoms are hidden while a dress is worn; there is nothing to cycle.
    return selection;
  }

  const key = category === 'Top' ? 'topId' : category === 'Bottom' ? 'bottomId' : 'shoesId';
  return { ...selection, [key]: nextId(garments.map((garment) => garment.id), selection[key], direction) };
}

/** Cycles the worn dress; only meaningful while a dress is selected. */
export function cycleDress(selection: ComposedSelection, dresses: GarmentItem[], direction: 1 | -1): ComposedSelection {
  if (!selection.dressId || dresses.length === 0) {
    return selection;
  }

  return { ...selection, dressId: nextId(dresses.map((dress) => dress.id), selection.dressId, direction) };
}

/** Cycles the worn hairstyle preset; disabled while the hairstyle is hidden. */
export function cycleHairstyle(selection: ComposedSelection, hairstyles: HairstylePreset[], direction: 1 | -1): ComposedSelection {
  if (!selection.hairstyleVisible || hairstyles.length === 0) {
    return selection;
  }

  return { ...selection, hairstyleId: nextId(hairstyles.map((preset) => preset.id), selection.hairstyleId, direction) };
}

function nextId(ids: string[], currentId: string | undefined, direction: 1 | -1): string {
  const currentIndex = currentId ? ids.indexOf(currentId) : -1;
  if (currentIndex < 0) {
    return ids[0];
  }

  return ids[(currentIndex + direction + ids.length) % ids.length];
}

/**
 * Selects/unselects a dress. Selecting remembers the exact top/bottom being worn and hides
 * them; unselecting restores exactly those two and nothing else. Switching between dresses
 * keeps the original remembered pair.
 */
export function toggleDress(selection: ComposedSelection, dressId: string): ComposedSelection {
  if (selection.dressId === dressId) {
    return unselectDress(selection);
  }

  if (selection.dressId) {
    return { ...selection, dressId };
  }

  return {
    ...selection,
    dressId,
    rememberedTopId: selection.topId,
    rememberedBottomId: selection.bottomId,
    topId: undefined,
    bottomId: undefined
  };
}

export function unselectDress(selection: ComposedSelection): ComposedSelection {
  if (!selection.dressId) {
    return selection;
  }

  return {
    ...selection,
    dressId: undefined,
    topId: selection.rememberedTopId,
    bottomId: selection.rememberedBottomId,
    rememberedTopId: undefined,
    rememberedBottomId: undefined
  };
}

/** Adds/removes an accessory; at most MAX_ACCESSORIES are worn, extra adds are ignored. */
export function toggleAccessory(selection: ComposedSelection, accessoryId: string): ComposedSelection {
  if (selection.accessoryIds.includes(accessoryId)) {
    return { ...selection, accessoryIds: selection.accessoryIds.filter((id) => id !== accessoryId) };
  }

  if (selection.accessoryIds.length >= MAX_ACCESSORIES) {
    return selection;
  }

  return { ...selection, accessoryIds: [...selection.accessoryIds, accessoryId] };
}

export function toggleBag(selection: ComposedSelection, bagId: string): ComposedSelection {
  return { ...selection, bagId: selection.bagId === bagId ? undefined : bagId };
}

export function toggleOuterwear(selection: ComposedSelection, outerwearId: string): ComposedSelection {
  return { ...selection, outerwearId: selection.outerwearId === outerwearId ? undefined : outerwearId };
}

export function toggleHairstyleVisibility(selection: ComposedSelection): ComposedSelection {
  return { ...selection, hairstyleVisible: !selection.hairstyleVisible };
}

/** Garment ids the composed figure is wearing — exactly what gets saved on the outfit. */
export function deriveGarmentIds(selection: ComposedSelection): string[] {
  const wornBase = selection.dressId ? [selection.dressId] : [selection.topId, selection.bottomId];
  return [...wornBase, selection.shoesId, selection.outerwearId, selection.bagId, ...selection.accessoryIds]
    .filter((id): id is string => Boolean(id));
}

/**
 * Bridges the Wardrobe quick-build map (one garment per category) into a composed selection.
 * A picked dress is worn while the picked top/bottom are remembered (so unselecting the dress in
 * the Builder restores them). Base Top/Bottom/Shoes auto-fill is intentionally left to
 * `ensureComposedDefaults` at Builder render time, so unpicked base slots fall back to the first
 * item of their category.
 */
export function composedSelectionFromCategoryMap(map: Partial<Record<GarmentCategory, string>>): ComposedSelection {
  const selection: ComposedSelection = {
    ...EMPTY_COMPOSED_SELECTION,
    accessoryIds: map.Accessory ? [map.Accessory] : [],
    shoesId: map.Shoes,
    outerwearId: map.Outerwear,
    bagId: map.Bag
  };

  if (map.Dress) {
    selection.dressId = map.Dress;
    selection.rememberedTopId = map.Top;
    selection.rememberedBottomId = map.Bottom;
  } else {
    selection.topId = map.Top;
    selection.bottomId = map.Bottom;
  }

  return selection;
}

/** Rebuilds the composed selection from a saved outfit so editing shows exactly what was saved. */
export function composedSelectionFromOutfit(outfit: Outfit): ComposedSelection {
  const selection: ComposedSelection = {
    ...EMPTY_COMPOSED_SELECTION,
    accessoryIds: [],
    hairstyleId: outfit.hairstylePresetId ?? undefined,
    hairstyleVisible: outfit.hairstyleVisible ?? true
  };

  for (const item of outfit.items) {
    switch (item.category) {
      case 'Top':
        selection.topId = item.garmentId;
        break;
      case 'Bottom':
        selection.bottomId = item.garmentId;
        break;
      case 'Dress':
        selection.dressId = item.garmentId;
        break;
      case 'Shoes':
        selection.shoesId = item.garmentId;
        break;
      case 'Outerwear':
        selection.outerwearId = item.garmentId;
        break;
      case 'Bag':
        selection.bagId = item.garmentId;
        break;
      case 'Accessory':
        if (selection.accessoryIds.length < MAX_ACCESSORIES) {
          selection.accessoryIds = [...selection.accessoryIds, item.garmentId];
        }
        break;
    }
  }

  return selection;
}
