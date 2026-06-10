import type { paths } from './schema';

type JsonResponse<Operation, Status extends number> = Operation extends { responses: infer Responses }
  ? Status extends keyof Responses
    ? Responses[Status] extends { content: { 'application/json': infer Body } }
      ? Body
      : never
    : never
  : never;

type ArrayItem<T> = T extends readonly (infer Item)[] ? Item : never;

export type BodyReferencePhoto = ArrayItem<JsonResponse<paths['/api/body-reference-photos']['get'], 200>>;
export type GarmentItem = JsonResponse<paths['/api/garments/{garmentId}']['get'], 200>;
export type Outfit = JsonResponse<paths['/api/outfits/{outfitId}']['get'], 200>;
export type ScheduledOutfit = ArrayItem<JsonResponse<paths['/api/schedule']['get'], 200>>;
export type TryOnJob = JsonResponse<paths['/api/try-on-jobs/{jobId}']['get'], 200>;
export type SharedOutfit = JsonResponse<paths['/api/share/{token}']['get'], 200>;

export type CreatedGarment = JsonResponse<paths['/api/garments']['post'], 201>;
export type CreatedBodyReferencePhoto = JsonResponse<paths['/api/body-reference-photos']['post'], 201>;
export type CreatedOutfit = JsonResponse<paths['/api/outfits']['post'], 201>;
export type StartedTryOnJob = JsonResponse<paths['/api/outfits/{outfitId}/try-on']['post'], 202>;

export type GarmentCategory = GarmentItem['category'];
export type BodyZone = GarmentItem['bodyZone'];
export type LaundryStatus = GarmentItem['laundryStatus'];
export type TryOnStatus = TryOnJob['status'];
export type OutfitItem = ArrayItem<Outfit['items']>;
