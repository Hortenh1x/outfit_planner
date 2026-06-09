export type GarmentCategory = 'Top' | 'Bottom';
export type BodyZone = 'Torso' | 'Legs';
export type PreviewMode = 'clothes' | 'person';
export type TryOnStatus = 'Queued' | 'Processing' | 'Succeeded' | 'Failed';
export type LaundryStatus = 'clean' | 'worn' | 'washing';

export interface GarmentItem {
  id: string;
  userId: string;
  name: string;
  category: GarmentCategory;
  bodyZone: BodyZone;
  imageUrl: string;
  thumbnailUrl: string;
  tags: string[];
  primaryColor?: string | null;
  secondaryColors?: string[];
  material?: string | null;
  brand?: string | null;
  size?: string | null;
  season?: string[];
  weatherMinTemp?: number | null;
  weatherMaxTemp?: number | null;
  occasion?: string[];
  formalityScore?: number | null;
  warmthScore?: number | null;
  comfortScore?: number | null;
  isFavorite?: boolean;
  isArchived?: boolean;
  lastWornAt?: string | null;
  laundryStatus?: LaundryStatus;
  createdAt: string;
}

export interface BodyReferencePhoto {
  id: string;
  userId: string;
  imageUrl: string;
  createdAt: string;
}

export interface OutfitItem {
  garmentId: string;
  name: string;
  category: GarmentCategory;
  bodyZone: BodyZone;
  thumbnailUrl: string;
}

export interface Outfit {
  id: string;
  userId?: string;
  name: string;
  items: OutfitItem[];
  tags?: string[];
  occasion?: string[];
  isFavorite?: boolean;
  isArchived?: boolean;
  clothesOnlyPreviewUrl?: string | null;
  personPreviewUrl?: string | null;
  createdAt: string;
}

export interface ScheduledOutfit {
  id: string;
  userId: string;
  date: string;
  outfitId: string;
  createdAt: string;
}

export interface TryOnJob {
  id: string;
  userId: string;
  outfitId: string;
  bodyReferencePhotoUrl: string;
  status: TryOnStatus;
  providerJobId?: string | null;
  outputImageUrl?: string | null;
  error?: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface OutfitSelection {
  topId?: string;
  bottomId?: string;
}
