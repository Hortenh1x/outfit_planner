import type {
  BodyReferencePhoto,
  GarmentCategory,
  GarmentItem,
  LaundryStatus,
  Outfit,
  ScheduledOutfit,
  ShareLinkResponse,
  SharedOutfit,
  TryOnCostEstimate,
  TryOnJob,
  TryOnMode
} from '../types';

const apiBaseUrl = import.meta.env.VITE_API_URL ?? '/api';
const csrfCookieName = 'outfit_csrf';

interface ApiErrorBody {
  error?: string;
  detail?: string;
  traceId?: string;
}

export interface HealthStatus {
  status: string;
  service: string;
}

export interface SystemStatus {
  api: string;
  storage: string;
  postgres: unknown;
  aiProvider: string;
}

export interface AuthProvider {
  id: string;
  label: string;
  configured: boolean;
  flow: 'password' | 'oauth' | 'oidc';
}

export interface AuthUser {
  id: string;
  email?: string | null;
  displayName: string;
}

export interface AuthSession {
  user: AuthUser;
  expiresAt: string;
}

export interface GarmentFilters {
  category?: GarmentCategory;
  color?: string;
  season?: string;
  q?: string;
  sort?: 'recent' | 'oldest' | 'name' | 'category';
  offset?: number;
  limit?: number;
  favorite?: boolean;
  archived?: boolean;
  occasion?: string;
  brand?: string;
  material?: string;
}

export interface OutfitFilters {
  q?: string;
  occasion?: string;
  favorite?: boolean;
  archived?: boolean;
  sort?: 'recent' | 'oldest' | 'name';
  offset?: number;
  limit?: number;
}

type QueryFunctionLikeContext = {
  queryKey: unknown;
  signal?: AbortSignal;
  client?: unknown;
};

export interface GarmentMetadataInput {
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
}

export type UpdateGarmentInput = Partial<{
  name: string;
  category: GarmentCategory;
  tags: string[];
}> & GarmentMetadataInput;

export type UpdateOutfitInput = Partial<{
  name: string;
  garmentIds: string[];
  tags: string[];
  occasion: string[];
  isFavorite: boolean;
  isArchived: boolean;
}>;

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const method = init?.method ?? 'GET';
  const url = `${apiBaseUrl}${path}`;
  const headers = new Headers(init?.headers);
  if (!headers.has('Content-Type') && init?.body && !(init.body instanceof FormData)) {
    headers.set('Content-Type', 'application/json');
  }

  const csrfToken = readCookie(csrfCookieName);
  if (requiresCsrf(method) && csrfToken && !headers.has('X-CSRF-Token')) {
    headers.set('X-CSRF-Token', csrfToken);
  }

  const response = await fetchWithDiagnostics(url, {
    ...init,
    credentials: 'include',
    headers
  }, method, path);

  if (!response.ok) {
    throw await createApiError(response, method, path);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

function buildQuery(filters?: object): string {
  if (!filters) {
    return '';
  }

  const params = new URLSearchParams();
  Object.entries(filters).forEach(([key, value]) => {
    if (value !== undefined) {
      params.set(key, String(value));
    }
  });

  const query = params.toString();
  return query ? `?${query}` : '';
}

function requestFilters<T extends object>(filters?: T | QueryFunctionLikeContext): T | undefined {
  if (!filters || isQueryFunctionContext(filters)) {
    return undefined;
  }

  return filters;
}

function isQueryFunctionContext(value: object): value is QueryFunctionLikeContext {
  return 'queryKey' in value || 'signal' in value || 'client' in value;
}

export function listGarments(filters?: GarmentFilters | QueryFunctionLikeContext): Promise<GarmentItem[]> {
  return request<GarmentItem[]>(`/garments${buildQuery(requestFilters(filters))}`);
}

export function getGarment(garmentId: string): Promise<GarmentItem> {
  return request<GarmentItem>(`/garments/${garmentId}`);
}

export function getHealth(): Promise<HealthStatus> {
  return request<HealthStatus>('/health');
}

export function getSystemStatus(): Promise<SystemStatus> {
  return request<SystemStatus>('/system/status');
}

export function getAuthProviders(): Promise<AuthProvider[]> {
  return request<AuthProvider[]>('/auth/providers');
}

export function register(input: { email: string; password: string; repeatPassword: string }): Promise<AuthSession> {
  return request<AuthSession>('/auth/register', {
    method: 'POST',
    body: JSON.stringify(input)
  });
}

export function login(input: { email: string; password: string }): Promise<AuthSession> {
  return request<AuthSession>('/auth/login', {
    method: 'POST',
    body: JSON.stringify(input)
  });
}

export function logout(): Promise<void> {
  return request<void>('/auth/logout', {
    method: 'POST'
  });
}

export function getCurrentSession(): Promise<AuthSession> {
  return request<AuthSession>('/auth/me');
}

export function buildExternalAuthUrl(provider: 'google' | 'apple', returnUrl = '/builder'): string {
  return `${apiBaseUrl}/auth/external/${provider}/start?returnUrl=${encodeURIComponent(returnUrl)}`;
}

export function listBodyReferencePhotos(): Promise<BodyReferencePhoto[]> {
  return request<BodyReferencePhoto[]>('/body-reference-photos');
}

export function createBodyReferencePhoto(imageUrl: string): Promise<BodyReferencePhoto> {
  return request<BodyReferencePhoto>('/body-reference-photos', {
    method: 'POST',
    body: JSON.stringify({ imageUrl })
  });
}

export function deleteBodyReferencePhoto(photoId: string): Promise<void> {
  return request<void>(`/body-reference-photos/${photoId}`, {
    method: 'DELETE'
  });
}

export interface UploadedPhotoResponse {
  fileName: string;
  contentType: string;
  length: number;
  url: string;
  originalUrl?: string | null;
  thumbnailUrl?: string | null;
  cutoutUrl?: string | null;
  maskUrl?: string | null;
}

export async function uploadGarmentPhoto(file: File): Promise<UploadedPhotoResponse> {
  return uploadPhoto('/uploads/garment-photo', file);
}

export async function uploadBodyReferencePhoto(file: File): Promise<UploadedPhotoResponse> {
  return uploadPhoto('/uploads/body-reference-photo', file);
}

async function uploadPhoto(path: string, file: File): Promise<UploadedPhotoResponse> {
  const formData = new FormData();
  formData.append('file', file);

  const method = 'POST';
  const url = `${apiBaseUrl}${path}`;
  const headers = new Headers();
  const csrfToken = readCookie(csrfCookieName);
  if (csrfToken) {
    headers.set('X-CSRF-Token', csrfToken);
  }

  const response = await fetchWithDiagnostics(url, {
    method: 'POST',
    credentials: 'include',
    headers,
    body: formData
  }, method, path);

  if (!response.ok) {
    throw await createApiError(response, method, path);
  }

  return (await response.json()) as UploadedPhotoResponse;
}

async function fetchWithDiagnostics(url: string, init: RequestInit, method: string, path: string): Promise<Response> {
  logApiRequest(url, init, method, path);

  try {
    return await fetch(url, init);
  } catch (error) {
    const browserMessage = error instanceof Error ? error.message : String(error);
    const onlineStatus = typeof navigator === 'undefined' ? 'unknown' : String(navigator.onLine);
    const origin = typeof window === 'undefined' ? 'test' : window.location.origin;
    throw new Error(
      `Network request failed while calling ${method} ${url} failed. ` +
      `Browser error: ${browserMessage}. ` +
      `Origin: ${origin}. Browser online: ${onlineStatus}. ` +
      `Open DevTools > Network and check ${method} ${url}. ` +
      `If the request is missing, the browser blocked it before it reached the API/proxy.`
    );
  }
}

function logApiRequest(url: string, init: RequestInit, method: string, path: string) {
  if (typeof console === 'undefined') {
    return;
  }

  const body = init.body;
  const file = body instanceof FormData ? body.get('file') : null;
  console.info('[OutfitPlanner API]', {
    method,
    path,
    url,
    hasBody: Boolean(body),
    fileName: file instanceof File ? file.name : undefined,
    fileSize: file instanceof File ? file.size : undefined,
    fileType: file instanceof File ? file.type : undefined
  });
}

function requiresCsrf(method: string): boolean {
  return !['GET', 'HEAD', 'OPTIONS'].includes(method.toUpperCase());
}

function readCookie(name: string): string | null {
  if (typeof document === 'undefined') {
    return null;
  }

  const prefix = `${name}=`;
  const cookie = document.cookie
    .split(';')
    .map((part) => part.trim())
    .find((part) => part.startsWith(prefix));

  return cookie ? decodeURIComponent(cookie.slice(prefix.length)) : null;
}

async function createApiError(response: Response, method: string, path: string): Promise<Error> {
  const body = await readErrorBody(response);
  const traceId = response.headers.get('X-Trace-Id') ?? body.traceId;
  const detail = body.error ?? body.detail ?? 'No error body returned.';
  const statusText = response.statusText ? ` ${response.statusText}` : '';
  const traceSuffix = traceId ? ` Trace id: ${traceId}.` : '';

  return new Error(`${method} ${apiBaseUrl}${path} failed with HTTP ${response.status}${statusText}: ${detail}.${traceSuffix}`);
}

async function readErrorBody(response: Response): Promise<ApiErrorBody> {
  try {
    return (await response.json()) as ApiErrorBody;
  } catch {
    return {};
  }
}

export function createGarment(input: {
  name: string;
  category: GarmentCategory;
  imageUrl: string;
  thumbnailUrl?: string;
  tags: string[];
} & GarmentMetadataInput): Promise<GarmentItem> {
  return request<GarmentItem>('/garments', {
    method: 'POST',
    body: JSON.stringify(input)
  });
}

export function updateGarment(garmentId: string, input: UpdateGarmentInput): Promise<GarmentItem> {
  return request<GarmentItem>(`/garments/${garmentId}`, {
    method: 'PATCH',
    body: JSON.stringify(input)
  });
}

export function deleteGarment(garmentId: string): Promise<void> {
  return request<void>(`/garments/${garmentId}`, {
    method: 'DELETE'
  });
}

export function listOutfits(filters?: OutfitFilters | QueryFunctionLikeContext): Promise<Outfit[]> {
  return request<Outfit[]>(`/outfits${buildQuery(requestFilters(filters))}`);
}

export function getOutfit(outfitId: string): Promise<Outfit> {
  return request<Outfit>(`/outfits/${outfitId}`);
}

export function createOutfit(input: { name: string; garmentIds: string[] }): Promise<Outfit> {
  return request<Outfit>('/outfits', {
    method: 'POST',
    body: JSON.stringify(input)
  });
}

export function updateOutfit(outfitId: string, input: UpdateOutfitInput): Promise<Outfit> {
  return request<Outfit>(`/outfits/${outfitId}`, {
    method: 'PATCH',
    body: JSON.stringify(input)
  });
}

export function deleteOutfit(outfitId: string): Promise<void> {
  return request<void>(`/outfits/${outfitId}`, {
    method: 'DELETE'
  });
}

export function deleteOutfitTryOnPreview(outfitId: string): Promise<void> {
  return request<void>(`/outfits/${outfitId}/try-on-preview`, {
    method: 'DELETE'
  });
}

export function estimateTryOn(input: {
  outfitId: string;
  bodyReferencePhotoUrl?: string;
  bodyReferencePhotoId?: string;
  tryOnMode: TryOnMode;
}): Promise<TryOnCostEstimate> {
  return request<TryOnCostEstimate>(`/outfits/${input.outfitId}/try-on/estimate`, {
    method: 'POST',
    body: JSON.stringify({
      bodyReferencePhotoUrl: input.bodyReferencePhotoUrl,
      bodyReferencePhotoId: input.bodyReferencePhotoId,
      tryOnMode: input.tryOnMode
    })
  });
}

export function startTryOn(input: {
  outfitId: string;
  bodyReferencePhotoUrl?: string;
  bodyReferencePhotoId?: string;
  consentAccepted: boolean;
  tryOnMode: TryOnMode;
  confirmedCredits: number;
  confirmedCacheKey: string;
}): Promise<TryOnJob> {
  return request<TryOnJob>(`/outfits/${input.outfitId}/try-on`, {
    method: 'POST',
    body: JSON.stringify({
      bodyReferencePhotoUrl: input.bodyReferencePhotoUrl,
      bodyReferencePhotoId: input.bodyReferencePhotoId,
      consentAccepted: input.consentAccepted,
      tryOnMode: input.tryOnMode,
      confirmedCredits: input.confirmedCredits,
      confirmedCacheKey: input.confirmedCacheKey
    })
  });
}

export function getTryOnJob(jobId: string): Promise<TryOnJob> {
  return request<TryOnJob>(`/try-on-jobs/${jobId}`);
}

export function deleteTryOnJobOutput(jobId: string): Promise<void> {
  return request<void>(`/try-on-jobs/${jobId}/output`, {
    method: 'DELETE'
  });
}

export function scheduleOutfit(input: { date: string; outfitId: string }): Promise<ScheduledOutfit> {
  return request<ScheduledOutfit>('/schedule', {
    method: 'POST',
    body: JSON.stringify(input)
  });
}

export function listSchedule(from: string, to: string): Promise<ScheduledOutfit[]> {
  return request<ScheduledOutfit[]>(`/schedule?from=${from}&to=${to}`);
}

export function unscheduleOutfit(date: string): Promise<void> {
  return request<void>(`/schedule/${encodeURIComponent(date)}`, {
    method: 'DELETE'
  });
}

export function shareOutfit(outfitId: string): Promise<ShareLinkResponse> {
  return request<ShareLinkResponse>(`/outfits/${outfitId}/share`, {
    method: 'POST'
  });
}

export function revokeShare(token: string): Promise<void> {
  return request<void>(`/share/${encodeURIComponent(token)}`, {
    method: 'DELETE'
  });
}

export function getSharedOutfit(token: string): Promise<SharedOutfit> {
  return request<SharedOutfit>(`/share/${token}`);
}
