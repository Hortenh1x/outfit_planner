import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  buildExternalAuthUrl,
  createBodyReferencePhoto,
  deleteBodyReferencePhoto,
  deleteGarment,
  deleteOutfit,
  getGarment,
  getCurrentSession,
  getAuthProviders,
  getHealth,
  getOutfit,
  getSystemStatus,
  getTryOnJob,
  listGarments,
  listOutfits,
  login,
  logout,
  register,
  revokeShare,
  startTryOn,
  unscheduleOutfit,
  updateGarment,
  updateOutfit,
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
      bodyReferencePhotoId: 'body-1',
      consentAccepted: true,
      sequentialFlowEnabled: true
    });

    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe('/api/outfits/outfit-1/try-on');
    expect(init).toMatchObject({ credentials: 'include' });
    expect(JSON.parse(init?.body as string)).toMatchObject({
      bodyReferencePhotoUrl: 'https://example.com/body.jpg',
      bodyReferencePhotoId: 'body-1',
      consentAccepted: true,
      sequentialFlowEnabled: true
    });
  });

  it('uploads and stores a reusable body reference photo', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch')
      .mockResolvedValueOnce(
        new Response(JSON.stringify({ url: 'http://localhost:5000/api/storage/signed/body-reference-photos/original/body.png?expires=1&signature=sig' }), {
          status: 201,
          headers: { 'Content-Type': 'application/json' }
        })
      )
      .mockResolvedValueOnce(
        new Response(JSON.stringify({ id: 'body-1', imageUrl: 'http://localhost:5000/api/storage/signed/body-reference-photos/original/body.png?expires=1&signature=sig' }), {
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
      imageUrl: 'http://localhost:5000/api/storage/signed/body-reference-photos/original/body.png?expires=1&signature=sig'
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

  it('calls edit detail filtering unschedule and revoke endpoints', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch')
      .mockResolvedValueOnce(new Response(JSON.stringify([]), { status: 200, headers: { 'Content-Type': 'application/json' } }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ id: 'garment-1' }), { status: 200, headers: { 'Content-Type': 'application/json' } }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ id: 'garment-1', name: 'black shirt' }), { status: 200, headers: { 'Content-Type': 'application/json' } }))
      .mockResolvedValueOnce(new Response(JSON.stringify([]), { status: 200, headers: { 'Content-Type': 'application/json' } }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ id: 'outfit-1' }), { status: 200, headers: { 'Content-Type': 'application/json' } }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ id: 'outfit-1', name: 'office' }), { status: 200, headers: { 'Content-Type': 'application/json' } }))
      .mockResolvedValueOnce(new Response(null, { status: 204 }))
      .mockResolvedValueOnce(new Response(null, { status: 204 }))
      .mockResolvedValueOnce(new Response(null, { status: 204 }));

    await listGarments({ category: 'Top', color: 'black', season: 'summer', q: 'shirt', sort: 'recent', favorite: true, limit: 20 });
    await getGarment('garment-1');
    await updateGarment('garment-1', { name: 'black shirt', primaryColor: 'black', season: ['summer'], laundryStatus: 'worn' });
    await listOutfits({ q: 'office', occasion: 'business', favorite: true });
    await getOutfit('outfit-1');
    await updateOutfit('outfit-1', { name: 'office', occasion: ['business'], isFavorite: true });
    await deleteOutfit('outfit-1');
    await unscheduleOutfit('2026-06-08');
    await revokeShare('share-token');

    expect(fetchMock.mock.calls[0][0]).toBe('/api/garments?category=Top&color=black&season=summer&q=shirt&sort=recent&favorite=true&limit=20');
    expect(fetchMock.mock.calls[1][0]).toBe('/api/garments/garment-1');
    expect(fetchMock.mock.calls[2][0]).toBe('/api/garments/garment-1');
    expect(fetchMock.mock.calls[2][1]).toMatchObject({ method: 'PATCH' });
    expect(JSON.parse(fetchMock.mock.calls[2][1]?.body as string)).toMatchObject({ primaryColor: 'black', laundryStatus: 'worn' });
    expect(fetchMock.mock.calls[3][0]).toBe('/api/outfits?q=office&occasion=business&favorite=true');
    expect(fetchMock.mock.calls[4][0]).toBe('/api/outfits/outfit-1');
    expect(fetchMock.mock.calls[5][0]).toBe('/api/outfits/outfit-1');
    expect(fetchMock.mock.calls[5][1]).toMatchObject({ method: 'PATCH' });
    expect(fetchMock.mock.calls[6][0]).toBe('/api/outfits/outfit-1');
    expect(fetchMock.mock.calls[6][1]).toMatchObject({ method: 'DELETE' });
    expect(fetchMock.mock.calls[7][0]).toBe('/api/schedule/2026-06-08');
    expect(fetchMock.mock.calls[8][0]).toBe('/api/share/share-token');
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
