import type { GarmentFilters, GarmentMetadataInput } from '../../api/client';
import type { GarmentCategory, GarmentItem } from '../../types';

export type WardrobeCategoryFilter = GarmentCategory | 'All';

export interface WardrobeFilterState {
  q: string;
  category: WardrobeCategoryFilter;
  color: string;
  season: string;
  tag: string;
  favorite: boolean;
  archived: boolean;
  sort: NonNullable<GarmentFilters['sort']>;
}

export const defaultWardrobeFilters: WardrobeFilterState = {
  q: '',
  category: 'All',
  color: '',
  season: '',
  tag: '',
  favorite: false,
  archived: false,
  sort: 'recent'
};

export function toGarmentFilters(filters: WardrobeFilterState): GarmentFilters {
  return {
    ...(filters.q.trim() ? { q: filters.q.trim() } : {}),
    ...(filters.category !== 'All' ? { category: filters.category } : {}),
    ...(filters.color.trim() ? { color: filters.color.trim() } : {}),
    ...(filters.season.trim() ? { season: filters.season.trim() } : {}),
    ...(filters.favorite ? { favorite: true } : {}),
    archived: filters.archived,
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

export function duplicateGarmentInput(garment: GarmentItem): {
  name: string;
  category: GarmentCategory;
  imageUrl: string;
  thumbnailUrl?: string;
  tags: string[];
} & GarmentMetadataInput {
  return {
    name: `${garment.name} copy`,
    category: garment.category,
    imageUrl: garment.imageUrl,
    thumbnailUrl: garment.thumbnailUrl,
    tags: [...garment.tags],
    primaryColor: garment.primaryColor,
    secondaryColors: [...(garment.secondaryColors ?? [])],
    material: garment.material,
    brand: garment.brand,
    size: garment.size,
    season: [...(garment.season ?? [])],
    weatherMinTemp: normalizeNullableNumber(garment.weatherMinTemp),
    weatherMaxTemp: normalizeNullableNumber(garment.weatherMaxTemp),
    occasion: [...(garment.occasion ?? [])],
    formalityScore: normalizeNullableNumber(garment.formalityScore),
    warmthScore: normalizeNullableNumber(garment.warmthScore),
    comfortScore: normalizeNullableNumber(garment.comfortScore),
    isFavorite: false,
    isArchived: false,
    laundryStatus: garment.laundryStatus
  };
}

function normalizeTag(tag: string): string {
  return tag.trim().toLowerCase();
}

function normalizeNullableNumber(value: string | number | null | undefined): number | null {
  if (typeof value === 'number') {
    return Number.isFinite(value) ? value : null;
  }

  if (typeof value === 'string') {
    const parsed = Number(value.trim());
    return Number.isFinite(parsed) ? parsed : null;
  }

  return null;
}
