import { describe, expect, it } from 'vitest';
import type { GarmentItem } from '../../types';
import {
  defaultWardrobeFilters,
  duplicateGarmentInput,
  filterGarmentsByLocalTags,
  toGarmentFilters
} from './wardrobeFilters';

const baseGarment: GarmentItem = {
  id: 'garment-1',
  userId: 'user-1',
  name: 'Black silk cami',
  category: 'Top',
  bodyZone: 'Torso',
  imageUrl: '/uploads/black-silk-cami.png',
  thumbnailUrl: '/uploads/black-silk-cami-thumb.png',
  tags: ['silk', 'evening'],
  primaryColor: 'black',
  secondaryColors: ['cream'],
  material: 'silk',
  brand: 'studio',
  size: 'S',
  season: ['summer'],
  weatherMinTemp: 18,
  weatherMaxTemp: 30,
  occasion: ['date night'],
  formalityScore: 4,
  warmthScore: 1,
  comfortScore: 4,
  isFavorite: true,
  isArchived: false,
  lastWornAt: '2026-06-01T12:00:00Z',
  laundryStatus: 'clean',
  rotationDegrees: 0,
  createdAt: '2026-06-01T12:00:00Z'
};

describe('wardrobeFilters', () => {
  it('defaults to recent unarchived garments', () => {
    expect(defaultWardrobeFilters).toEqual({
      q: '',
      category: 'All',
      color: '',
      season: '',
      tag: '',
      favorite: false,
      sort: 'recent'
    });
    expect(toGarmentFilters(defaultWardrobeFilters)).toEqual({ archived: false, sort: 'recent' });
  });

  it('converts active UI filters into API garment filters', () => {
    expect(toGarmentFilters({
      q: 'silk',
      category: 'Top',
      color: 'black',
      season: 'summer',
      tag: 'evening',
      favorite: true,
      sort: 'name'
    })).toEqual({
      q: 'silk',
      category: 'Top',
      color: 'black',
      season: 'summer',
      favorite: true,
      archived: false,
      sort: 'name'
    });
  });

  it('filters locally by tag when a tag chip is active', () => {
    const garments = [
      baseGarment,
      { ...baseGarment, id: 'garment-2', name: 'Trench coat', tags: ['rain'], primaryColor: 'beige' }
    ];

    expect(filterGarmentsByLocalTags(garments, 'evening')).toEqual([baseGarment]);
    expect(filterGarmentsByLocalTags(garments, '')).toEqual(garments);
  });

  it('builds a safe duplicate payload without worn state', () => {
    expect(duplicateGarmentInput(baseGarment)).toEqual({
      name: 'Black silk cami copy',
      category: 'Top',
      imageUrl: '/uploads/black-silk-cami.png',
      thumbnailUrl: '/uploads/black-silk-cami-thumb.png',
      tags: ['silk', 'evening'],
      primaryColor: 'black',
      secondaryColors: ['cream'],
      material: 'silk',
      brand: 'studio',
      size: 'S',
      season: ['summer'],
      weatherMinTemp: 18,
      weatherMaxTemp: 30,
      occasion: ['date night'],
      formalityScore: 4,
      warmthScore: 1,
      comfortScore: 4,
      isFavorite: false,
      isArchived: false,
      laundryStatus: 'clean'
    });
  });

  it('normalizes numeric response strings when duplicating a garment', () => {
    expect(duplicateGarmentInput({
      ...baseGarment,
      weatherMinTemp: '18',
      weatherMaxTemp: '30',
      formalityScore: '4',
      warmthScore: '1',
      comfortScore: '4'
    })).toMatchObject({
      weatherMinTemp: 18,
      weatherMaxTemp: 30,
      formalityScore: 4,
      warmthScore: 1,
      comfortScore: 4
    });
  });
});
