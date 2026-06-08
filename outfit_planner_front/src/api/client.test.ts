import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  buildExternalAuthUrl,
  createBodyReferencePhoto,
  deleteBodyReferencePhoto,
  deleteGarment,
  getCurrentSession,
  getAuthProviders,
  getHealth,
  getSystemStatus,
  getTryOnJob,
  listGarments,
  login,
  logout,
  register,
  startTryOn,
  uploadBodyReferencePhoto
} from './client';

describe('api client', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('sends the sequential flow option when starting try-on generation', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      new Response(JSON.stringify({ id: 'job-1', outputImageUrl: 'https://example.com/out.png' }), {
        status: 202,
        headers: { 'Content-Type': 'application/json' }
      })
    );

    await startTryOn({
      outfitId: 'outfit-1',
      bodyReferencePhotoUrl: 'https://example.com/body.jpg',
      consentAccepted: true,
      sequentialFlowEnabled: true
    });

    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe('/api/outfits/outfit-1/try-on');
    expect(init).toMatchObject({ credentials: 'include' });
    expect(JSON.parse(init?.body as string)).toMatchObject({
      bodyReferencePhotoUrl: 'https://example.com/body.jpg',
      consentAccepted: true,
      sequentialFlowEnabled: true
    });
  });

  it('uploads and stores a reusable body reference photo', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch')
      .mockResolvedValueOnce(
        new Response(JSON.stringify({ url: 'http://localhost:5000/uploads/body-reference-photos/body.png' }), {
          status: 201,
          headers: { 'Content-Type': 'application/json' }
        })
      )
      .mockResolvedValueOnce(
        new Response(JSON.stringify({ id: 'body-1', imageUrl: 'http://localhost:5000/uploads/body-reference-photos/body.png' }), {
          status: 201,
          headers: { 'Content-Type': 'application/json' }
        })
      );

    const uploaded = await uploadBodyReferencePhoto(new File(['body'], 'body.png', { type: 'image/png' }));
    await createBodyReferencePhoto(uploaded.url);

    expect(fetchMock.mock.calls[0][0]).toContain('/uploads/body-reference-photo');
    expect(fetchMock.mock.calls[0][1]).toMatchObject({ credentials: 'include' });
    expect(fetchMock.mock.calls[1][0]).toContain('/body-reference-photos');
    expect(fetchMock.mock.calls[1][1]).toMatchObject({ credentials: 'include' });
    expect(JSON.parse(fetchMock.mock.calls[1][1]?.body as string)).toMatchObject({
      imageUrl: 'http://localhost:5000/uploads/body-reference-photos/body.png'
    });
  });

  it('explains browser-level fetch failures with request context', async () => {
    const consoleInfo = vi.spyOn(console, 'info').mockImplementation(() => undefined);
    vi.spyOn(globalThis, 'fetch').mockRejectedValue(new TypeError('Failed to fetch'));

    await expect(uploadBodyReferencePhoto(new File(['body'], 'body.png', { type: 'image/png' })))
      .rejects
      .toThrow(/Network request failed while calling POST \/api\/uploads\/body-reference-photo .*Failed to fetch.*DevTools.*Network/i);
    expect(consoleInfo).toHaveBeenCalledWith('[OutfitPlanner API]', expect.objectContaining({
      method: 'POST',
      path: '/uploads/body-reference-photo',
      url: '/api/uploads/body-reference-photo'
    }));
  });

  it('includes status and trace id for api error responses', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      new Response(JSON.stringify({ error: 'database unavailable' }), {
        status: 503,
        statusText: 'Service Unavailable',
        headers: {
          'Content-Type': 'application/json',
          'X-Trace-Id': 'trace-123'
        }
      })
    );

    await expect(listGarments())
      .rejects
      .toThrow(/GET \/api\/garments failed with HTTP 503 Service Unavailable.*database unavailable.*trace-123/i);
  });

  it('deletes wardrobe and body reference records without requiring a response body', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch')
      .mockResolvedValueOnce(new Response(null, { status: 204 }))
      .mockResolvedValueOnce(new Response(null, { status: 204 }));

    await deleteGarment('garment-1');
    await deleteBodyReferencePhoto('body-1');

    expect(fetchMock).toHaveBeenCalledWith('/api/garments/garment-1', expect.objectContaining({ method: 'DELETE' }));
    expect(fetchMock).toHaveBeenCalledWith('/api/body-reference-photos/body-1', expect.objectContaining({ method: 'DELETE' }));
  });

  it('covers service metadata and try-on status endpoints', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch')
      .mockResolvedValueOnce(new Response(JSON.stringify({ status: 'ok', service: 'outfit-planner-api' }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' }
      }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ api: 'running', storage: 'InMemory', postgres: null, aiProvider: 'Mock' }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' }
      }))
      .mockResolvedValueOnce(new Response(JSON.stringify([{ id: 'google', label: 'Google', configured: false, flow: 'oauth' }]), {
        status: 200,
        headers: { 'Content-Type': 'application/json' }
      }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ id: 'job-1', status: 'Succeeded' }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' }
      }));

    await getHealth();
    await getSystemStatus();
    await getAuthProviders();
    await getTryOnJob('job-1');

    expect(fetchMock).toHaveBeenCalledWith('/api/health', expect.objectContaining({ credentials: 'include', headers: expect.any(Object) }));
    expect(fetchMock).toHaveBeenCalledWith('/api/system/status', expect.objectContaining({ credentials: 'include', headers: expect.any(Object) }));
    expect(fetchMock).toHaveBeenCalledWith('/api/auth/providers', expect.objectContaining({ credentials: 'include', headers: expect.any(Object) }));
    expect(fetchMock).toHaveBeenCalledWith('/api/try-on-jobs/job-1', expect.objectContaining({ credentials: 'include', headers: expect.any(Object) }));
  });

  it('uses secure cookie-backed auth endpoints', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch')
      .mockResolvedValueOnce(new Response(JSON.stringify({ user: { id: 'usr_1', email: 'ada@example.com', displayName: 'ada' }, expiresAt: '2026-06-22T12:00:00Z' }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' }
      }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ user: { id: 'usr_1', email: 'ada@example.com', displayName: 'ada' }, expiresAt: '2026-06-22T12:00:00Z' }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' }
      }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ user: { id: 'usr_1', email: 'ada@example.com', displayName: 'ada' }, expiresAt: '2026-06-22T12:00:00Z' }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' }
      }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ status: 'signed-out' }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' }
      }));

    await register({ email: 'ada@example.com', password: 'correct horse battery', repeatPassword: 'correct horse battery' });
    await login({ email: 'ada@example.com', password: 'correct horse battery' });
    await getCurrentSession();
    await logout();

    expect(fetchMock).toHaveBeenCalledWith('/api/auth/register', expect.objectContaining({ method: 'POST', credentials: 'include' }));
    expect(fetchMock).toHaveBeenCalledWith('/api/auth/login', expect.objectContaining({ method: 'POST', credentials: 'include' }));
    expect(fetchMock).toHaveBeenCalledWith('/api/auth/me', expect.objectContaining({ credentials: 'include' }));
    expect(fetchMock).toHaveBeenCalledWith('/api/auth/logout', expect.objectContaining({ method: 'POST', credentials: 'include' }));
    expect(JSON.parse(fetchMock.mock.calls[0][1]?.body as string)).toMatchObject({
      email: 'ada@example.com',
      password: 'correct horse battery',
      repeatPassword: 'correct horse battery'
    });
    expect(buildExternalAuthUrl('google', '/builder')).toBe('/api/auth/external/google/start?returnUrl=%2Fbuilder');
  });
});
