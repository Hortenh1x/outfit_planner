import type { BodyReferencePhoto, GarmentCategory, GarmentItem, Outfit, ScheduledOutfit, TryOnJob } from '../types';

const apiBaseUrl = import.meta.env.VITE_API_URL ?? '/api';
const demoUser = 'demo-user';

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
  demoHeader?: string;
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const method = init?.method ?? 'GET';
  const url = `${apiBaseUrl}${path}`;
  const response = await fetchWithDiagnostics(url, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      'X-Demo-User': demoUser,
      ...init?.headers
    }
  }, method, path);

  if (!response.ok) {
    throw await createApiError(response, method, path);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

export function listGarments(): Promise<GarmentItem[]> {
  return request<GarmentItem[]>('/garments');
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
  const response = await fetchWithDiagnostics(url, {
    method: 'POST',
    headers: {
      'X-Demo-User': demoUser
    },
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
}): Promise<GarmentItem> {
  return request<GarmentItem>('/garments', {
    method: 'POST',
    body: JSON.stringify(input)
  });
}

export function deleteGarment(garmentId: string): Promise<void> {
  return request<void>(`/garments/${garmentId}`, {
    method: 'DELETE'
  });
}

export function listOutfits(): Promise<Outfit[]> {
  return request<Outfit[]>('/outfits');
}

export function createOutfit(input: { name: string; garmentIds: string[] }): Promise<Outfit> {
  return request<Outfit>('/outfits', {
    method: 'POST',
    body: JSON.stringify(input)
  });
}

export function startTryOn(input: {
  outfitId: string;
  bodyReferencePhotoUrl: string;
  consentAccepted: boolean;
  sequentialFlowEnabled: boolean;
}): Promise<TryOnJob> {
  return request<TryOnJob>(`/outfits/${input.outfitId}/try-on`, {
    method: 'POST',
    body: JSON.stringify({
      bodyReferencePhotoUrl: input.bodyReferencePhotoUrl,
      consentAccepted: input.consentAccepted,
      sequentialFlowEnabled: input.sequentialFlowEnabled
    })
  });
}

export function getTryOnJob(jobId: string): Promise<TryOnJob> {
  return request<TryOnJob>(`/try-on-jobs/${jobId}`);
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

export function shareOutfit(outfitId: string): Promise<{ token: string; url: string }> {
  return request<{ token: string; url: string }>(`/outfits/${outfitId}/share`, {
    method: 'POST'
  });
}

export function getSharedOutfit(token: string): Promise<Outfit> {
  return request<Outfit>(`/share/${token}`);
}
