import type { components, paths } from './schema';

type JsonBody<Response> = Response extends { content: { 'application/json': infer Body } } ? Body : never;

type ResponseBody<Operation, Status extends number> = Operation extends { responses: infer Responses }
  ? Status extends keyof Responses
    ? JsonBody<Responses[Status]>
    : never
  : never;

type ArrayItem<T> = T extends readonly (infer Item)[] ? Item : never;
type GeneratedOrFallback<Generated, Fallback> = [Generated] extends [never] ? Fallback : Generated;

type GarmentListItem = ArrayItem<ResponseBody<paths['/api/garments']['get'], 200>>;
type BodyReferencePhotoListItem = ArrayItem<ResponseBody<paths['/api/body-reference-photos']['get'], 200>>;
type OutfitListItem = ArrayItem<ResponseBody<paths['/api/outfits']['get'], 200>>;
type ScheduledOutfitListItem = ArrayItem<ResponseBody<paths['/api/schedule']['get'], 200>>;

type BodyReferencePhotoFallback = {
  id: string;
  userId: string;
  imageUrl: string;
  createdAt: string;
};

type GarmentItemFallback = {
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
};

type OutfitItemFallback = {
  garmentId: string;
  name: string;
  category: GarmentCategory;
  bodyZone: BodyZone;
  thumbnailUrl: string;
};

type OutfitFallback = {
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
};

type ScheduledOutfitFallback = {
  id: string;
  userId: string;
  date: string;
  outfitId: string;
  createdAt: string;
};

type TryOnJobFallback = {
  id: string;
  userId: string;
  outfitId: string;
  bodyReferencePhotoUrl: string;
  sequentialFlowEnabled?: boolean;
  status: TryOnStatus;
  providerJobId?: string | null;
  providerName?: string | null;
  providerRequestId?: string | null;
  sourceBodyPhotoId?: string | null;
  outputImageUrl?: string | null;
  consentAcceptedAt?: string | null;
  retentionUntil?: string | null;
  isDeleted?: boolean;
  error?: string | null;
  createdAt: string;
  updatedAt: string;
};

type SharedOutfitFallback = Omit<OutfitFallback, 'userId'>;

export type GarmentCategory = Exclude<components['schemas']['GarmentCategory'], null>;
export type BodyZone = 'Torso' | 'Legs' | 'FullBody' | 'Feet' | 'Head' | 'Hands' | 'Accessory' | 'OuterLayer';
export type LaundryStatus = 'clean' | 'worn' | 'washing';
export type TryOnStatus = 'Queued' | 'Processing' | 'Succeeded' | 'Failed';

export type BodyReferencePhoto = GeneratedOrFallback<BodyReferencePhotoListItem, BodyReferencePhotoFallback>;
export type GarmentItem = GeneratedOrFallback<GarmentListItem, GarmentItemFallback>;
export type OutfitItem = GeneratedOrFallback<ArrayItem<NonNullable<OutfitListItem>['items']>, OutfitItemFallback>;
export type Outfit = GeneratedOrFallback<OutfitListItem, OutfitFallback>;
export type ScheduledOutfit = GeneratedOrFallback<ScheduledOutfitListItem, ScheduledOutfitFallback>;
export type TryOnJob = GeneratedOrFallback<ResponseBody<paths['/api/try-on-jobs/{jobId}']['get'], 200>, TryOnJobFallback>;
export type SharedOutfit = GeneratedOrFallback<ResponseBody<paths['/api/share/{token}']['get'], 200>, SharedOutfitFallback>;

export type CreatedGarment = GeneratedOrFallback<ResponseBody<paths['/api/garments']['post'], 200 | 201>, GarmentItem>;
export type CreatedBodyReferencePhoto = GeneratedOrFallback<
  ResponseBody<paths['/api/body-reference-photos']['post'], 200 | 201>,
  BodyReferencePhoto
>;
export type CreatedOutfit = GeneratedOrFallback<ResponseBody<paths['/api/outfits']['post'], 200 | 201>, Outfit>;
export type StartedTryOnJob = GeneratedOrFallback<ResponseBody<paths['/api/outfits/{outfitId}/try-on']['post'], 200 | 202>, TryOnJob>;
