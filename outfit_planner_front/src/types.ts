export type {
  BodyReferencePhoto,
  BodyZone,
  CreatedBodyReferencePhoto,
  CreatedGarment,
  CreatedOutfit,
  GarmentCategory,
  GarmentItem,
  LaundryStatus,
  Outfit,
  OutfitItem,
  ScheduledOutfit,
  SharedOutfit,
  StartedTryOnJob,
  TryOnJob,
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
  hatId?: string;
}
