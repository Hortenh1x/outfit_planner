import { describe, expect, it } from 'vitest';
import { groupGarmentsByCategory, selectionLabel } from './outfitUtils';
import type { GarmentItem } from '../../types';

const garments: GarmentItem[] = [
  {
    id: 'top-1',
    userId: 'demo-user',
    name: 'White tee',
    category: 'Top',
    bodyZone: 'Torso',
    imageUrl: '/white-tee.jpg',
    thumbnailUrl: '/white-tee.jpg',
    tags: ['cotton'],
    primaryColor: null,
    secondaryColors: [],
    material: null,
    brand: null,
    size: null,
    season: [],
    weatherMinTemp: null,
    weatherMaxTemp: null,
    occasion: [],
    formalityScore: null,
    warmthScore: null,
    comfortScore: null,
    isFavorite: false,
    isArchived: false,
    lastWornAt: null,
    laundryStatus: 'clean',
    rotationDegrees: 0,
    createdAt: '2026-05-21T12:00:00Z'
  },
  {
    id: 'bottom-1',
    userId: 'demo-user',
    name: 'Blue jeans',
    category: 'Bottom',
    bodyZone: 'Legs',
    imageUrl: '/jeans.jpg',
    thumbnailUrl: '/jeans.jpg',
    tags: ['denim'],
    primaryColor: null,
    secondaryColors: [],
    material: null,
    brand: null,
    size: null,
    season: [],
    weatherMinTemp: null,
    weatherMaxTemp: null,
    occasion: [],
    formalityScore: null,
    warmthScore: null,
    comfortScore: null,
    isFavorite: false,
    isArchived: false,
    lastWornAt: null,
    laundryStatus: 'clean',
    rotationDegrees: 0,
    createdAt: '2026-05-21T12:00:00Z'
  }
];

describe('outfit builder utilities', () => {
  it('groups garments into expanded category slots', () => {
    const grouped = groupGarmentsByCategory(garments);

    expect(grouped.Top).toHaveLength(1);
    expect(grouped.Bottom).toHaveLength(1);
    expect(grouped.Dress).toHaveLength(0);
    expect(grouped.Shoes).toHaveLength(0);
    expect(grouped.Top[0].bodyZone).toBe('Torso');
    expect(grouped.Bottom[0].bodyZone).toBe('Legs');
  });

  it('describes incomplete and complete outfit selections', () => {
    expect(selectionLabel({ topId: undefined, bottomId: undefined }, garments)).toBe('Choose outfit pieces');
    expect(selectionLabel({ topId: 'top-1', bottomId: undefined }, garments)).toBe('White tee + choose another piece');
    expect(selectionLabel({ topId: 'top-1', bottomId: 'bottom-1' }, garments)).toBe('White tee + Blue jeans');
  });
});
