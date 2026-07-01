import type { GarmentFilters } from '../../api/client';
import type { GarmentCategory, GarmentItem } from '../../types';

export type WardrobeCategoryFilter = GarmentCategory | 'All';

export interface WardrobeFilterState {
  q: string;
  category: WardrobeCategoryFilter;
  color: string;
  season: string;
  tag: string;
  sort: NonNullable<GarmentFilters['sort']>;
}

export const defaultWardrobeFilters: WardrobeFilterState = {
  q: '',
  category: 'All',
  color: '',
  season: '',
  tag: '',
  sort: 'recent'
};

export function toGarmentFilters(filters: WardrobeFilterState): GarmentFilters {
  return {
    ...(filters.q.trim() ? { q: filters.q.trim() } : {}),
    ...(filters.category !== 'All' ? { category: filters.category } : {}),
    ...(filters.color.trim() ? { color: filters.color.trim() } : {}),
    ...(filters.season.trim() ? { season: filters.season.trim() } : {}),
    archived: false,
    sort: filters.sort
  };
}

export function filterGarmentsByLocalTags(garments: GarmentItem[], tag: string): GarmentItem[] {
  const normalizedTag = normalizeTag(tag);
  if (!normalizedTag) {
    return garments;
  }

  return garments.filter((garment) => garment.tags.some((garmentTag) => normalizeTag(garmentTag) === normalizedTag));
}

function normalizeTag(tag: string): string {
  return tag.trim().toLowerCase();
}
