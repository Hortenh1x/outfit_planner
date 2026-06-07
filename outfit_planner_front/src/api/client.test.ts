import { afterEach, describe, expect, it, vi } from 'vitest';
import { createBodyReferencePhoto, deleteBodyReferencePhoto, deleteGarment, listGarments, startTryOn, uploadBodyReferencePhoto } from './client';

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
    expect(fetchMock.mock.calls[1][0]).toContain('/body-reference-photos');
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
});
