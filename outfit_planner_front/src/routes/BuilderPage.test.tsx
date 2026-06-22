import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { cleanup, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { BuilderPage } from './BuilderPage';

function renderBuilder() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <BuilderPage />
      </MemoryRouter>
    </QueryClientProvider>
  );
}

describe('BuilderPage', () => {
  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
  });

  it('uploads missing wardrobe pieces directly from builder empty slots', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
      const url = String(input);

      if (url.includes('/uploads/garment-photo')) {
        return jsonResponse({ url: 'http://localhost:5000/uploads/garments/linen-shirt.png' }, 201);
      }

      if (url.endsWith('/garments') && init?.method === 'POST') {
        return jsonResponse({
          id: 'garment-1',
          name: 'linen shirt',
          category: 'Top',
          bodyZone: 'Torso',
          imageUrl: 'http://localhost:5000/uploads/garments/linen-shirt.png',
          thumbnailUrl: 'http://localhost:5000/uploads/garments/linen-shirt.png',
          tags: []
        }, 201);
      }

      return jsonResponse([]);
    });

    renderBuilder();

    const addTopInput = await screen.findByLabelText(/add a top in wardrobe/i);
    expect(addTopInput).toHaveAttribute('type', 'file');
    expect(screen.getByLabelText(/add a bottom in wardrobe/i)).toHaveAttribute('type', 'file');
    expect(screen.getByLabelText(/add body photo/i)).toHaveAttribute('type', 'file');
    expect(screen.queryByRole('button', { name: /^add$/i })).not.toBeInTheDocument();

    await userEvent.upload(addTopInput, new File(['shirt'], 'linen shirt.png', { type: 'image/png' }));

    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledWith(expect.stringContaining('/uploads/garment-photo'), expect.anything());
      expect(fetchMock).toHaveBeenCalledWith(expect.stringMatching(/\/garments$/), expect.objectContaining({ method: 'POST' }));
    });

    const createCall = fetchMock.mock.calls.find(([url, init]) => String(url).endsWith('/garments') && init?.method === 'POST');
    expect(JSON.parse(createCall?.[1]?.body as string)).toMatchObject({
      name: 'linen shirt',
      category: 'Top',
      imageUrl: 'http://localhost:5000/uploads/garments/linen-shirt.png',
      tags: []
    });
  });

  it('deletes body reference photos from the builder controls', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
      const url = String(input);

      if (url.endsWith('/body-reference-photos') && (!init || init.method === undefined)) {
        return jsonResponse([
          { id: 'body-1', imageUrl: 'http://localhost:5000/uploads/body-reference-photos/body.png' }
        ]);
      }

      if (url.endsWith('/body-reference-photos/body-1') && init?.method === 'DELETE') {
        return new Response(null, { status: 204 });
      }

      return jsonResponse([]);
    });

    renderBuilder();

    await userEvent.click(await screen.findByRole('button', { name: /delete body reference 1/i }));

    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledWith(expect.stringMatching(/\/body-reference-photos\/body-1$/), expect.objectContaining({ method: 'DELETE' }));
    });
  });

  it('does not show the AI try-on consent checkbox in builder controls', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse([]));

    renderBuilder();

    expect(await screen.findAllByRole('button', { name: /save outfit/i })).not.toHaveLength(0);
    expect(screen.queryByText(/I consent to AI try-on processing/i)).not.toBeInTheDocument();
  });

  it('renders real animated mode indicators', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse([]));

    const builder = renderBuilder();
    const modeToggle = builder.container.querySelector('.mode-toggle');

    expect(modeToggle).not.toBeNull();
    expect(await within(modeToggle as HTMLElement).findByRole('button', { name: /clothes only/i })).toBeInTheDocument();
    expect(builder.container.querySelector('.mode-toggle .toggle-motion-indicator')).toBeInTheDocument();
  });

  it('shows server-estimated cost and confirms before starting generation', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
      const url = String(input);

      if (url.endsWith('/garments')) {
        return jsonResponse([
          {
            id: 'top-1',
            userId: 'user-a',
            name: 'white tee',
            category: 'Top',
            bodyZone: 'Torso',
            imageUrl: '/top.png',
            thumbnailUrl: '/top.png',
            tags: [],
            secondaryColors: [],
            season: [],
            occasion: [],
            isFavorite: false,
            isArchived: false,
            laundryStatus: 'clean',
            createdAt: '2026-06-21T12:00:00Z'
          },
          {
            id: 'bag-1',
            userId: 'user-a',
            name: 'leather bag',
            category: 'Bag',
            bodyZone: 'Accessory',
            imageUrl: '/bag.png',
            thumbnailUrl: '/bag.png',
            tags: [],
            secondaryColors: [],
            season: [],
            occasion: [],
            isFavorite: false,
            isArchived: false,
            laundryStatus: 'clean',
            createdAt: '2026-06-21T12:00:00Z'
          }
        ]);
      }

      if (url.endsWith('/body-reference-photos')) {
        return jsonResponse([{ id: 'body-1', imageUrl: 'https://example.com/body.jpg', createdAt: '2026-06-21T12:00:00Z' }]);
      }

      if (url.endsWith('/outfits') && init?.method === 'POST') {
        return jsonResponse({
          id: 'outfit-1',
          name: 'Today',
          items: [
            { garmentId: 'top-1', name: 'white tee', category: 'Top', bodyZone: 'Torso', thumbnailUrl: '/top.png' },
            { garmentId: 'bag-1', name: 'leather bag', category: 'Bag', bodyZone: 'Accessory', thumbnailUrl: '/bag.png' }
          ],
          tags: [],
          occasion: [],
          isFavorite: false,
          isArchived: false,
          createdAt: '2026-06-21T12:00:00Z'
        }, 201);
      }

      if (url.endsWith('/outfits/outfit-1/try-on/estimate') && init?.method === 'POST') {
        return jsonResponse({
          mode: 'SequentialOutfitTryOn',
          provider: 'FashnTryOnProvider',
          bodyTryOnItems: [{ garmentId: 'top-1', name: 'white tee', category: 'Top', bodyZone: 'Torso', thumbnailUrl: '/top.png' }],
          visualOnlyItems: [{ garmentId: 'bag-1', name: 'leather bag', category: 'Bag', bodyZone: 'Accessory', thumbnailUrl: '/bag.png' }],
          includedGarmentIds: ['top-1'],
          excludedGarmentIds: ['bag-1'],
          estimatedCredits: 1,
          isAvailable: true,
          requiresAi: true,
          requiresPremiumConfirmation: false,
          cacheKey: 'cache-key-a',
          hasCachedResult: false,
          summary: 'Sequential outfit try-on will use 1 body garment run(s).',
          warnings: ['Shoes, bags, accessories, and hats are visual-only and will not be sent to AI in this mode.']
        });
      }

      if (url.endsWith('/outfits/outfit-1/try-on') && init?.method === 'POST') {
        return jsonResponse({ id: 'job-1', status: 'Queued' }, 202);
      }

      if (url.endsWith('/try-on-jobs/job-1')) {
        return jsonResponse({ id: 'job-1', status: 'Queued' });
      }

      return jsonResponse([]);
    });

    renderBuilder();

    await userEvent.click(await screen.findByRole('button', { name: /white tee/i }));
    await userEvent.click(await screen.findByRole('button', { name: /leather bag/i }));
    await userEvent.click(screen.getByRole('button', { name: /generate preview/i }));

    expect(await screen.findByText(/1 credit/i)).toBeInTheDocument();
    expect(screen.getByText((_, element) => element?.textContent === 'Visual-only: leather bag')).toBeInTheDocument();
    expect(fetchMock).not.toHaveBeenCalledWith(expect.stringMatching(/\/try-on$/), expect.anything());

    await userEvent.click(screen.getByRole('button', { name: /confirm generation/i }));

    const startCall = fetchMock.mock.calls.find(([url, init]) => String(url).endsWith('/outfits/outfit-1/try-on') && init?.method === 'POST');
    expect(startCall).toBeDefined();
    expect(JSON.parse(startCall?.[1]?.body as string)).toMatchObject({
      tryOnMode: 'SequentialOutfitTryOn',
      confirmedCredits: 1,
      confirmedCacheKey: 'cache-key-a'
    });
  });

  it('allows clothes-only preview without a body reference photo', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
      const url = String(input);

      if (url.endsWith('/garments')) {
        return jsonResponse([
          {
            id: 'top-1',
            userId: 'user-a',
            name: 'white tee',
            category: 'Top',
            bodyZone: 'Torso',
            imageUrl: '/top.png',
            thumbnailUrl: '/top.png',
            tags: [],
            secondaryColors: [],
            season: [],
            occasion: [],
            isFavorite: false,
            isArchived: false,
            laundryStatus: 'clean',
            createdAt: '2026-06-21T12:00:00Z'
          }
        ]);
      }

      if (url.endsWith('/body-reference-photos')) {
        return jsonResponse([]);
      }

      if (url.endsWith('/outfits') && init?.method === 'POST') {
        return jsonResponse({
          id: 'outfit-1',
          name: 'Today',
          items: [
            { garmentId: 'top-1', name: 'white tee', category: 'Top', bodyZone: 'Torso', thumbnailUrl: '/top.png' }
          ],
          tags: [],
          occasion: [],
          isFavorite: false,
          isArchived: false,
          createdAt: '2026-06-21T12:00:00Z'
        }, 201);
      }

      if (url.endsWith('/outfits/outfit-1/try-on/estimate') && init?.method === 'POST') {
        return jsonResponse({
          mode: 'ClothesOnlyPreview',
          provider: 'MockTryOnProvider',
          bodyTryOnItems: [{ garmentId: 'top-1', name: 'white tee', category: 'Top', bodyZone: 'Torso', thumbnailUrl: '/top.png' }],
          visualOnlyItems: [],
          includedGarmentIds: [],
          excludedGarmentIds: ['top-1'],
          estimatedCredits: 0,
          isAvailable: true,
          requiresAi: false,
          requiresPremiumConfirmation: false,
          cacheKey: 'cache-key-free',
          hasCachedResult: false,
          summary: 'Clothes-only preview is free.',
          warnings: []
        });
      }

      if (url.endsWith('/outfits/outfit-1/try-on') && init?.method === 'POST') {
        return jsonResponse({ id: 'job-free', status: 'Succeeded' }, 202);
      }

      if (url.endsWith('/try-on-jobs/job-free')) {
        return jsonResponse({ id: 'job-free', status: 'Succeeded' });
      }

      return jsonResponse([]);
    });

    const builder = renderBuilder();

    await userEvent.click(await screen.findByRole('button', { name: /white tee/i }));
    const tryOnModeSelector = builder.container.querySelector('.tryon-mode-selector');
    expect(tryOnModeSelector).not.toBeNull();
    await userEvent.click(within(tryOnModeSelector as HTMLElement).getByRole('button', { name: /clothes only/i }));

    const generateButton = screen.getByRole('button', { name: /generate preview/i });
    expect(generateButton).toBeEnabled();
    await userEvent.click(generateButton);

    expect(await screen.findByText((_, element) => element?.textContent === 'Free')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: /confirm generation/i }));

    const estimateCall = fetchMock.mock.calls.find(([url, init]) => String(url).endsWith('/outfits/outfit-1/try-on/estimate') && init?.method === 'POST');
    expect(JSON.parse(estimateCall?.[1]?.body as string)).toMatchObject({
      tryOnMode: 'ClothesOnlyPreview'
    });
    expect(JSON.parse(estimateCall?.[1]?.body as string)).not.toHaveProperty('bodyReferencePhotoUrl');

    const startCall = fetchMock.mock.calls.find(([url, init]) => String(url).endsWith('/outfits/outfit-1/try-on') && init?.method === 'POST');
    expect(startCall).toBeDefined();
    expect(JSON.parse(startCall?.[1]?.body as string)).toMatchObject({
      consentAccepted: false,
      tryOnMode: 'ClothesOnlyPreview',
      confirmedCredits: 0,
      confirmedCacheKey: 'cache-key-free'
    });
    expect(JSON.parse(startCall?.[1]?.body as string)).not.toHaveProperty('bodyReferencePhotoUrl');
  });

  it('clears the active saved outfit when the draft selection changes', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
      const url = String(input);

      if (url.endsWith('/garments')) {
        return jsonResponse([
          {
            id: 'top-1',
            name: 'white tee',
            category: 'Top',
            bodyZone: 'Torso',
            imageUrl: 'http://localhost:5000/uploads/garments/white.png',
            thumbnailUrl: 'http://localhost:5000/uploads/garments/white.png',
            tags: [],
            createdAt: '2026-06-09T12:00:00Z'
          },
          {
            id: 'top-2',
            name: 'black tee',
            category: 'Top',
            bodyZone: 'Torso',
            imageUrl: 'http://localhost:5000/uploads/garments/black.png',
            thumbnailUrl: 'http://localhost:5000/uploads/garments/black.png',
            tags: [],
            createdAt: '2026-06-09T12:00:00Z'
          }
        ]);
      }

      if (url.endsWith('/outfits')) {
        return jsonResponse([
          {
            id: 'outfit-1',
            name: 'Saved outfit',
            items: [{ garmentId: 'top-1', name: 'white tee', category: 'Top', bodyZone: 'Torso', thumbnailUrl: 'http://localhost:5000/uploads/garments/white.png' }],
            createdAt: '2026-06-09T12:00:00Z'
          }
        ]);
      }

      return jsonResponse([]);
    });

    renderBuilder();

    await userEvent.click(await screen.findByRole('button', { name: /saved outfit/i }));
    expect(screen.getByRole('button', { name: /share/i })).not.toBeDisabled();

    await userEvent.click(await screen.findByRole('button', { name: /black tee/i }));

    expect(screen.getByRole('button', { name: /share/i })).toBeDisabled();
  });
});

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' }
  });
}
