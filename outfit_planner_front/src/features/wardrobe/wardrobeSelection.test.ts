import { describe, expect, it } from 'vitest';
import {
  isGarmentSelected,
  toggleWardrobeSelection,
  wardrobeSelectionCount,
  type WardrobeBuildSelection
} from './wardrobeSelection';
import type { GarmentItem } from '../../types';

function garment(id: string, category: GarmentItem['category']): GarmentItem {
  return {
    id,
    name: id,
    category,
    imageUrl: `/img/${id}.png`,
    thumbnailUrl: `/thumb/${id}.png`
  } as GarmentItem;
}

describe('toggleWardrobeSelection', () => {
  it('adds an unselected garment under its category', () => {
    const selection = toggleWardrobeSelection({}, garment('top-1', 'Top'));
    expect(selection).toEqual({ Top: 'top-1' });
  });

  it('removes the garment when the same one is toggled again', () => {
    const selection = toggleWardrobeSelection({ Top: 'top-1' }, garment('top-1', 'Top'));
    expect(selection).toEqual({});
  });

  it('replaces the selection within the same category (one per category)', () => {
    const selection = toggleWardrobeSelection({ Top: 'top-1' }, garment('top-2', 'Top'));
    expect(selection).toEqual({ Top: 'top-2' });
    expect(wardrobeSelectionCount(selection)).toBe(1);
  });

  it('keeps garments from different categories side by side', () => {
    let selection: WardrobeBuildSelection = {};
    selection = toggleWardrobeSelection(selection, garment('top-1', 'Top'));
    selection = toggleWardrobeSelection(selection, garment('bottom-1', 'Bottom'));
    selection = toggleWardrobeSelection(selection, garment('shoes-1', 'Shoes'));
    expect(selection).toEqual({ Top: 'top-1', Bottom: 'bottom-1', Shoes: 'shoes-1' });
    expect(wardrobeSelectionCount(selection)).toBe(3);
  });

  it('does not mutate the input selection', () => {
    const original: WardrobeBuildSelection = { Top: 'top-1' };
    toggleWardrobeSelection(original, garment('bottom-1', 'Bottom'));
    expect(original).toEqual({ Top: 'top-1' });
  });
});

describe('isGarmentSelected', () => {
  it('is true only for the garment held in its category slot', () => {
    const selection: WardrobeBuildSelection = { Top: 'top-1' };
    expect(isGarmentSelected(selection, garment('top-1', 'Top'))).toBe(true);
    expect(isGarmentSelected(selection, garment('top-2', 'Top'))).toBe(false);
    expect(isGarmentSelected(selection, garment('bottom-1', 'Bottom'))).toBe(false);
  });
});

describe('wardrobeSelectionCount', () => {
  it('counts the number of selected categories', () => {
    expect(wardrobeSelectionCount({})).toBe(0);
    expect(wardrobeSelectionCount({ Top: 'top-1', Shoes: 'shoes-1' })).toBe(2);
  });
});
