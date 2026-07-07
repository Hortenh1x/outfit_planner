import type { paths } from './schema';

type JsonResponse<Operation, Status extends number> = Operation extends { responses: infer Responses }
  ? Status extends keyof Responses
    ? Responses[Status] extends { content: { 'application/json': infer Body } }
      ? Body
      : never
    : never
  : never;

type ArrayItem<T> = T extends readonly (infer Item)[] ? Item : never;
type Override<T, R> = Omit<T, keyof R> & R;

export type BodyReferencePhoto = ArrayItem<JsonResponse<paths['/api/body-reference-photos']['get'], 200>>;
export type HairstylePreset = Override<
  ArrayItem<JsonResponse<paths['/api/hairstyles']['get'], 200>>,
  { sortOrder: number }
>;
export type GarmentItem = Override<
  JsonResponse<paths['/api/garments/{garmentId}']['get'], 200>,
  {
    perceptualHash?: string | null;
    cutoutWidthPx?: number | null;
    cutoutHeightPx?: number | null;
  }
>;
type OutfitResponse = JsonResponse<paths['/api/outfits/{outfitId}']['get'], 200>;
export type OutfitItem = Override<
  ArrayItem<OutfitResponse['items']>,
  { cutoutWidthPx?: number | null; cutoutHeightPx?: number | null }
>;
// Composed-figure state shared by owner and shared outfit responses.
type ComposedOutfitOverrides = {
  items: OutfitItem[];
  hairstylePresetId?: string | null;
  hairstyleVisible?: boolean;
  silhouetteGender?: 'Male' | 'Female' | null;
  hairstyleAssetUrl?: string | null;
};
export type Outfit = Override<OutfitResponse, ComposedOutfitOverrides>;
export type ScheduledOutfit = ArrayItem<JsonResponse<paths['/api/schedule']['get'], 200>>;
export type TryOnJob = JsonResponse<paths['/api/try-on-jobs/{jobId}']['get'], 200>;
export type SharedOutfit = Override<JsonResponse<paths['/api/share/{token}']['get'], 200>, ComposedOutfitOverrides>;

export type CreatedGarment = JsonResponse<paths['/api/garments']['post'], 201>;
export type CreatedBodyReferencePhoto = JsonResponse<paths['/api/body-reference-photos']['post'], 201>;
export type CreatedOutfit = JsonResponse<paths['/api/outfits']['post'], 201>;
type TryOnCostEstimateResponse = JsonResponse<paths['/api/outfits/{outfitId}/try-on/estimate']['post'], 200>;
export type TryOnCostEstimate = Override<TryOnCostEstimateResponse, { estimatedCredits: number }>;
export type StartedTryOnJob = JsonResponse<paths['/api/outfits/{outfitId}/try-on']['post'], 202>;
export type ShareLinkResponse = JsonResponse<paths['/api/outfits/{outfitId}/share']['post'], 200>;

export type GarmentCategory = GarmentItem['category'];
export type BodyZone = GarmentItem['bodyZone'];
export type LaundryStatus = GarmentItem['laundryStatus'];
export type TryOnStatus = TryOnJob['status'];
export type TryOnMode = TryOnCostEstimate['mode'];
