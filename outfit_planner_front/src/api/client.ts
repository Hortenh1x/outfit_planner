import type {
  BodyReferencePhoto,
  GarmentCategory,
  GarmentItem,
  HairstylePreset,
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

// Typed error so callers can branch on the HTTP status and surface the trace id, instead of
// matching substrings of the message (e.g. `message.includes('HTTP 401')`).
export class ApiError extends Error {
  readonly status: number;
  readonly detail: string;
  readonly traceId?: string;

  constructor(message: string, status: number, detail: string, traceId?: string) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.detail = detail;
    this.traceId = traceId;
  }
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

export type UserGender = 'Male' | 'Female';

// Effective account role (pinned-by-email overrides applied on the server).
export type UserRole = 'Free' | 'Premium' | 'Admin';

export interface AuthUser {
  id: string;
  email?: string | null;
  displayName: string;
  username?: string | null;
  avatarUrl?: string | null;
  gender?: UserGender | null;
  role?: UserRole | null;
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
  rotationDegrees?: number | null;
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
  // Composed-figure state; null hairstylePresetId leaves the worn hairstyle unchanged, an
  // empty string clears it.
  hairstylePresetId: string | null;
  hairstyleVisible: boolean;
  silhouetteGender: UserGender | null;
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

// Global hairstyle presets, already filtered by the account's gender on the server; empty when
// the account has no gender set.
export function listHairstyles(): Promise<HairstylePreset[]> {
  return request<HairstylePreset[]>('/hairstyles');
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

export function updateAccountProfile(input: { username: string; gender: UserGender | null }): Promise<AuthSession> {
  return request<AuthSession>('/account/profile', {
    method: 'PATCH',
    body: JSON.stringify(input)
  });
}

export function uploadAccountAvatar(file: File): Promise<AuthSession> {
  const formData = new FormData();
  formData.append('file', file);
  return request<AuthSession>('/account/avatar', {
    method: 'POST',
    body: formData
  });
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
  perceptualHash?: string | null;
  // Alpha-bounding-box size of the processed cutout; null on the original-only fast path.
  cutoutWidthPx?: number | null;
  cutoutHeightPx?: number | null;
}

export async function uploadGarmentPhoto(file: File, signal?: AbortSignal): Promise<UploadedPhotoResponse> {
  return uploadPhoto('/uploads/garment-photo', file, signal);
}

// Fast path: stores the original + thumbnail only (no background removal). Removal then runs
// asynchronously on the server once the garment is created.
export async function uploadGarmentOriginal(file: File, signal?: AbortSignal): Promise<UploadedPhotoResponse> {
  return uploadPhoto('/uploads/garment-original', file, signal);
}

export async function uploadBodyReferencePhoto(file: File): Promise<UploadedPhotoResponse> {
  return uploadPhoto('/uploads/body-reference-photo', file);
}

export interface AutoTagColorSuggestion {
  name: string;
  hex: string;
  confidence: number;
}

export interface AutoTagSuggestion {
  value: string;
  confidence: number;
}

export interface GarmentAutoTagResponse {
  // False when the local auto-tag service is disabled or unreachable (empty suggestions).
  isAvailable: boolean;
  provider: string;
  category?: GarmentCategory | null;
  categoryConfidence: number;
  colors: AutoTagColorSuggestion[];
  seasons: AutoTagSuggestion[];
  tags: AutoTagSuggestion[];
}

// Requests metadata prefill suggestions for a freshly uploaded garment photo. Client-orchestrated
// after the row's cutout/original is ready (concurrency-limited + abortable, like eager removal).
// Always resolves; an unavailable tagger yields isAvailable=false with empty suggestions.
export async function classifyGarmentPhoto(
  imageUrl: string,
  knownTags: string[],
  signal?: AbortSignal
): Promise<GarmentAutoTagResponse> {
  return request<GarmentAutoTagResponse>('/uploads/garment-photo/classify', {
    method: 'POST',
    body: JSON.stringify({ imageUrl, knownTags }),
    signal
  });
}

async function uploadPhoto(path: string, file: File, signal?: AbortSignal): Promise<UploadedPhotoResponse> {
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
    body: formData,
    signal
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

async function createApiError(response: Response, method: string, path: string): Promise<ApiError> {
  const body = await readErrorBody(response);
  const traceId = response.headers.get('X-Trace-Id') ?? body.traceId ?? undefined;
  const detail = body.error ?? body.detail ?? 'No error body returned.';
  const statusText = response.statusText ? ` ${response.statusText}` : '';
  const traceSuffix = traceId ? ` Trace id: ${traceId}.` : '';

  return new ApiError(
    `${method} ${apiBaseUrl}${path} failed with HTTP ${response.status}${statusText}: ${detail}.${traceSuffix}`,
    response.status,
    detail,
    traceId
  );
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
  perceptualHash?: string | null;
  backgroundRemovalPending?: boolean;
  cutoutWidthPx?: number | null;
  cutoutHeightPx?: number | null;
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

export function createOutfit(input: {
  name: string;
  garmentIds: string[];
  hairstylePresetId?: string | null;
  hairstyleVisible?: boolean;
  silhouetteGender?: UserGender | null;
}): Promise<Outfit> {
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

// Paywall entitlements: the account's plan limits, usage against them, and AI-credit balance.

export interface AccountEntitlements {
  role: UserRole;
  maxGarments?: number | null;
  maxOutfits?: number | null;
  maxBodyReferencePhotos?: number | null;
  garmentCount: number;
  outfitCount: number;
  bodyReferencePhotoCount: number;
  creditsUnlimited: boolean;
  creditBalance: number;
  monthlyCreditAllowance: number;
  allowedAiModes: TryOnMode[];
  maxTryOnResolution: string;
  priorityTryOnQueue: boolean;
}

export const accountEntitlementsQueryKey = ['account-entitlements'] as const;

export function getAccountEntitlements(): Promise<AccountEntitlements> {
  return request<AccountEntitlements>('/account/entitlements');
}

// Admin panel API (requires the Admin role; non-admins receive 403).

export interface AdminStats {
  totalUsers: number;
  totalGarments: number;
  totalOutfits: number;
  totalTryOnJobs: number;
}

export interface AdminUser {
  id: string;
  email?: string | null;
  username: string;
  gender?: UserGender | null;
  role: UserRole;
  // Pinned accounts keep their role by email; the panel cannot change or delete them.
  rolePinned: boolean;
  createdAt: string;
  lastLoginAt?: string | null;
  emailVerifiedAt?: string | null;
  garmentCount: number;
  outfitCount: number;
  tryOnJobCount: number;
  bodyReferencePhotoCount: number;
  activeSessionCount: number;
  avatarUrl?: string | null;
  // Raw AI-credit balance; null for accounts with unlimited credits (Admin).
  creditBalance?: number | null;
}

export interface AdminUsersPage {
  items: AdminUser[];
  totalCount: number;
  offset: number;
  limit: number;
}

export interface AdminUsersFilters {
  q?: string;
  role?: UserRole;
  offset?: number;
  limit?: number;
}

export function getAdminStats(): Promise<AdminStats> {
  return request<AdminStats>('/admin/stats');
}

export function listAdminUsers(filters?: AdminUsersFilters | QueryFunctionLikeContext): Promise<AdminUsersPage> {
  return request<AdminUsersPage>(`/admin/users${buildQuery(requestFilters(filters))}`);
}

export function getAdminUser(userId: string): Promise<AdminUser> {
  return request<AdminUser>(`/admin/users/${encodeURIComponent(userId)}`);
}

export function updateAdminUserRole(userId: string, role: UserRole): Promise<AdminUser> {
  return request<AdminUser>(`/admin/users/${encodeURIComponent(userId)}/role`, {
    method: 'PUT',
    body: JSON.stringify({ role })
  });
}

export function revokeAdminUserSessions(userId: string): Promise<{ status: string }> {
  return request<{ status: string }>(`/admin/users/${encodeURIComponent(userId)}/sessions/revoke`, {
    method: 'POST'
  });
}

export function purgeAdminUserAiOutputs(userId: string): Promise<{ purged: number }> {
  return request<{ purged: number }>(`/admin/users/${encodeURIComponent(userId)}/purge-ai-outputs`, {
    method: 'POST'
  });
}

// Same shape as the self-service account export, with the account record sanitized.
export function getAdminUserExport(userId: string): Promise<unknown> {
  return request<unknown>(`/admin/users/${encodeURIComponent(userId)}/export`);
}

export function deleteAdminUser(userId: string): Promise<void> {
  return request<void>(`/admin/users/${encodeURIComponent(userId)}`, {
    method: 'DELETE'
  });
}

// Appends an AdminAdjustment row to the user's AI-credit ledger and returns the new balance.
export function adjustAdminUserCredits(userId: string, delta: number): Promise<{ balance: number }> {
  return request<{ balance: number }>(`/admin/users/${encodeURIComponent(userId)}/credits`, {
    method: 'POST',
    body: JSON.stringify({ delta })
  });
}
