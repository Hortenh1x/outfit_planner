export type {
  BodyReferencePhoto,
  BodyZone,
  CreatedBodyReferencePhoto,
  CreatedGarment,
  CreatedOutfit,
  GarmentCategory,
  GarmentItem,
  HairstylePreset,
  LaundryStatus,
  Outfit,
  OutfitItem,
  ScheduledOutfit,
  ShareLinkResponse,
  SharedOutfit,
  StartedTryOnJob,
  TryOnCostEstimate,
  TryOnJob,
  TryOnMode,
  TryOnStatus
} from './api/generated/responseTypes';

export type PreviewMode = 'clothes' | 'person';

export interface OutfitSelection {
  topId?: string;
  bottomId?: string;
  dressId?: string;
  outerwearId?: string;
  shoesId?: string;
  bagId?: string;
  accessoryId?: string;
}
