import { describe, expect, it } from 'vitest';
import {
  EMPTY_COMPOSED_SELECTION,
  MAX_ACCESSORIES,
  composedSelectionFromCategoryMap,
  composedSelectionFromOutfit,
  cycleCarousel,
  cycleDress,
  cycleHairstyle,
  deriveGarmentIds,
  ensureComposedDefaults,
  toggleAccessory,
  toggleBag,
  toggleDress,
  toggleHairstyleVisibility,
  toggleOuterwear,
  unselectDress,
  type ComposedSelection
} from './composedOutfit';
import type { GarmentItem, HairstylePreset, Outfit } from '../../types';

function garment(id: string, category: GarmentItem['category']): GarmentItem {
  return {
    id,
    name: id,
    category,
    imageUrl: `/img/${id}.png`,
    thumbnailUrl: `/thumb/${id}.png`
  } as GarmentItem;
}

function hairstyle(id: string): HairstylePreset {
  return { id, name: id, gender: 'Female', sortOrder: 1, assetUrl: `/api/hairstyles/assets/${id}.svg` } as HairstylePreset;
}

const base: ComposedSelection = { ...EMPTY_COMPOSED_SELECTION, accessoryIds: [] };

describe('ensureComposedDefaults', () => {
  it('starts each on-figure slot at the first item of its category', () => {
    const filled = ensureComposedDefaults(
      base,
      {
        Top: [garment('top-1', 'Top'), garment('top-2', 'Top')],
        Bottom: [garment('bottom-1', 'Bottom')],
        Shoes: [garment('shoes-1', 'Shoes')]
      },
      [hairstyle('hair-1'), hairstyle('hair-2')]
    );

    expect(filled.topId).toBe('top-1');
    expect(filled.bottomId).toBe('bottom-1');
    expect(filled.shoesId).toBe('shoes-1');
    expect(filled.hairstyleId).toBe('hair-1');
  });

  it('leaves empty categories as empty slots and keeps valid choices', () => {
    const kept = ensureComposedDefaults(
      { ...base, topId: 'top-2' },
      { Top: [garment('top-1', 'Top'), garment('top-2', 'Top')] },
      []
    );

    expect(kept.topId).toBe('top-2');
    expect(kept.bottomId).toBeUndefined();
    expect(kept.shoesId).toBeUndefined();
    expect(kept.hairstyleId).toBeUndefined();
  });

  it('does not resurrect top/bottom while a dress is worn', () => {
    const withDress = ensureComposedDefaults(
      { ...base, dressId: 'dress-1' },
      { Top: [garment('top-1', 'Top')], Bottom: [garment('bottom-1', 'Bottom')] },
      []
    );

    expect(withDress.topId).toBeUndefined();
    expect(withDress.bottomId).toBeUndefined();
  });
});

describe('carousel cycling', () => {
  const tops = [garment('top-1', 'Top'), garment('top-2', 'Top'), garment('top-3', 'Top')];

  it('cycles forward and wraps around to the first item', () => {
    let selection: ComposedSelection = { ...base, topId: 'top-3' };
    selection = cycleCarousel(selection, 'Top', tops, 1);
    expect(selection.topId).toBe('top-1');
  });

  it('cycles backward and wraps around to the last item', () => {
    let selection: ComposedSelection = { ...base, topId: 'top-1' };
    selection = cycleCarousel(selection, 'Top', tops, -1);
    expect(selection.topId).toBe('top-3');
  });

  it('ignores top/bottom cycling while a dress is worn', () => {
    const withDress = { ...base, dressId: 'dress-1' };
    expect(cycleCarousel(withDress, 'Top', tops, 1)).toBe(withDress);
  });

  it('is a no-op when the category has no garments', () => {
    expect(cycleCarousel(base, 'Shoes', [], 1)).toBe(base);
  });
});

describe('dress rules', () => {
  const worn = { ...base, topId: 'top-2', bottomId: 'bottom-1', shoesId: 'shoes-1' };

  it('selecting a dress hides top and bottom and remembers them', () => {
    const withDress = toggleDress(worn, 'dress-1');

    expect(withDress.dressId).toBe('dress-1');
    expect(withDress.topId).toBeUndefined();
    expect(withDress.bottomId).toBeUndefined();
    expect(withDress.rememberedTopId).toBe('top-2');
    expect(withDress.rememberedBottomId).toBe('bottom-1');
    expect(withDress.shoesId).toBe('shoes-1');
  });

  it('unselecting the dress restores exactly the remembered top and bottom', () => {
    const restored = unselectDress(toggleDress(worn, 'dress-1'));

    expect(restored.dressId).toBeUndefined();
    expect(restored.topId).toBe('top-2');
    expect(restored.bottomId).toBe('bottom-1');
    expect(restored.rememberedTopId).toBeUndefined();
  });

  it('switching dresses keeps the originally remembered top and bottom', () => {
    const switched = toggleDress(toggleDress(worn, 'dress-1'), 'dress-2');
    expect(switched.dressId).toBe('dress-2');

    const restored = unselectDress(switched);
    expect(restored.topId).toBe('top-2');
    expect(restored.bottomId).toBe('bottom-1');
  });

  it('tapping the worn dress unselects it', () => {
    const restored = toggleDress(toggleDress(worn, 'dress-1'), 'dress-1');
    expect(restored.dressId).toBeUndefined();
    expect(restored.topId).toBe('top-2');
  });

  it('does not affect anything except top and bottom', () => {
    const dressed = toggleDress({ ...worn, bagId: 'bag-1', accessoryIds: ['acc-1'], hairstyleId: 'hair-1' }, 'dress-1');

    expect(dressed.shoesId).toBe('shoes-1');
    expect(dressed.bagId).toBe('bag-1');
    expect(dressed.accessoryIds).toEqual(['acc-1']);
    expect(dressed.hairstyleId).toBe('hair-1');
  });

  it('cycles the worn dress with wrap-around', () => {
    const dresses = [garment('dress-1', 'Dress'), garment('dress-2', 'Dress')];
    const withDress = toggleDress(worn, 'dress-2');

    expect(cycleDress(withDress, dresses, 1).dressId).toBe('dress-1');
    expect(cycleDress(base, dresses, 1)).toBe(base);
  });
});

describe('side pieces', () => {
  it('caps accessories at three and ignores further adds', () => {
    let selection = base;
    for (const id of ['acc-1', 'acc-2', 'acc-3', 'acc-4']) {
      selection = toggleAccessory(selection, id);
    }

    expect(selection.accessoryIds).toEqual(['acc-1', 'acc-2', 'acc-3']);
    expect(selection.accessoryIds).toHaveLength(MAX_ACCESSORIES);
  });

  it('removes an accessory when tapped again', () => {
    const selection = toggleAccessory(toggleAccessory(base, 'acc-1'), 'acc-1');
    expect(selection.accessoryIds).toEqual([]);
  });

  it('keeps at most one bag, replacing or unselecting it', () => {
    const withBag = toggleBag(base, 'bag-1');
    expect(withBag.bagId).toBe('bag-1');
    expect(toggleBag(withBag, 'bag-2').bagId).toBe('bag-2');
    expect(toggleBag(withBag, 'bag-1').bagId).toBeUndefined();
  });

  it('toggles outerwear like a side piece', () => {
    const withCoat = toggleOuterwear(base, 'coat-1');
    expect(withCoat.outerwearId).toBe('coat-1');
    expect(toggleOuterwear(withCoat, 'coat-1').outerwearId).toBeUndefined();
  });
});

describe('hairstyle rules', () => {
  const presets = [hairstyle('hair-1'), hairstyle('hair-2')];

  it('cycles hairstyles with wrap-around while visible', () => {
    const selection = { ...base, hairstyleId: 'hair-2', hairstyleVisible: true };
    expect(cycleHairstyle(selection, presets, 1).hairstyleId).toBe('hair-1');
  });

  it('cannot be cycled while hidden', () => {
    const hidden = toggleHairstyleVisibility({ ...base, hairstyleId: 'hair-1' });
    expect(hidden.hairstyleVisible).toBe(false);
    expect(cycleHairstyle(hidden, presets, 1)).toBe(hidden);
  });

  it('a second visibility toggle brings the same hairstyle back', () => {
    const roundTrip = toggleHairstyleVisibility(toggleHairstyleVisibility({ ...base, hairstyleId: 'hair-2' }));
    expect(roundTrip.hairstyleVisible).toBe(true);
    expect(roundTrip.hairstyleId).toBe('hair-2');
  });
});

describe('saving and restoring', () => {
  it('derives dress XOR top/bottom plus all side pieces', () => {
    const withDress: ComposedSelection = {
      ...base,
      dressId: 'dress-1',
      shoesId: 'shoes-1',
      outerwearId: 'coat-1',
      bagId: 'bag-1',
      accessoryIds: ['acc-1', 'acc-2'],
      rememberedTopId: 'top-1',
      rememberedBottomId: 'bottom-1'
    };

    expect(deriveGarmentIds(withDress)).toEqual(['dress-1', 'shoes-1', 'coat-1', 'bag-1', 'acc-1', 'acc-2']);

    const withSeparates: ComposedSelection = { ...base, topId: 'top-1', bottomId: 'bottom-1', shoesId: 'shoes-1' };
    expect(deriveGarmentIds(withSeparates)).toEqual(['top-1', 'bottom-1', 'shoes-1']);
  });

  it('bridges a wardrobe category map into a composed selection (separates)', () => {
    const selection = composedSelectionFromCategoryMap({ Top: 'top-1', Bottom: 'bottom-1', Shoes: 'shoes-1' });

    expect(selection.topId).toBe('top-1');
    expect(selection.bottomId).toBe('bottom-1');
    expect(selection.shoesId).toBe('shoes-1');
    expect(selection.dressId).toBeUndefined();
    expect(selection.accessoryIds).toEqual([]);
    expect(selection.hairstyleVisible).toBe(true);
  });

  it('bridges a dress + top/bottom map so the dress is worn and the pair is remembered', () => {
    const selection = composedSelectionFromCategoryMap({
      Dress: 'dress-1',
      Top: 'top-1',
      Bottom: 'bottom-1',
      Shoes: 'shoes-1'
    });

    expect(selection.dressId).toBe('dress-1');
    expect(selection.topId).toBeUndefined();
    expect(selection.bottomId).toBeUndefined();
    expect(selection.rememberedTopId).toBe('top-1');
    expect(selection.rememberedBottomId).toBe('bottom-1');
    expect(selection.shoesId).toBe('shoes-1');
  });

  it('bridges side pieces and a single accessory', () => {
    const selection = composedSelectionFromCategoryMap({
      Outerwear: 'coat-1',
      Bag: 'bag-1',
      Accessory: 'acc-1'
    });

    expect(selection.outerwearId).toBe('coat-1');
    expect(selection.bagId).toBe('bag-1');
    expect(selection.accessoryIds).toEqual(['acc-1']);
  });

  it('bridges an empty map into the empty composed selection', () => {
    const selection = composedSelectionFromCategoryMap({});

    expect(selection.topId).toBeUndefined();
    expect(selection.dressId).toBeUndefined();
    expect(selection.accessoryIds).toEqual([]);
    expect(selection.hairstyleVisible).toBe(true);
  });

  it('rebuilds the selection from a saved outfit', () => {
    const outfit = {
      id: 'outfit-1',
      name: 'saved',
      hairstylePresetId: 'hair-2',
      hairstyleVisible: false,
      items: [
        { garmentId: 'dress-1', category: 'Dress' },
        { garmentId: 'shoes-1', category: 'Shoes' },
        { garmentId: 'bag-1', category: 'Bag' },
        { garmentId: 'acc-1', category: 'Accessory' },
        { garmentId: 'acc-2', category: 'Accessory' }
      ]
    } as unknown as Outfit;

    const selection = composedSelectionFromOutfit(outfit);

    expect(selection.dressId).toBe('dress-1');
    expect(selection.topId).toBeUndefined();
    expect(selection.shoesId).toBe('shoes-1');
    expect(selection.bagId).toBe('bag-1');
    expect(selection.accessoryIds).toEqual(['acc-1', 'acc-2']);
    expect(selection.hairstyleId).toBe('hair-2');
    expect(selection.hairstyleVisible).toBe(false);
  });
});
