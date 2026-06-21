import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { WardrobePage } from './WardrobePage';

const garmentsResponse = [
  {
    id: 'garment-1',
    userId: 'user-1',
    name: 'Black silk cami',
    category: 'Top',
    bodyZone: 'Torso',
    imageUrl: '/uploads/black-silk-cami.png',
    thumbnailUrl: '/uploads/black-silk-cami.png',
    tags: ['silk', 'evening'],
    primaryColor: 'black',
    secondaryColors: [],
    material: 'silk',
    brand: null,
    size: null,
    season: ['summer'],
    weatherMinTemp: null,
    weatherMaxTemp: null,
    occasion: [],
    formalityScore: null,
    warmthScore: null,
    comfortScore: null,
    isFavorite: false,
    isArchived: false,
    lastWornAt: null,
    laundryStatus: 'clean',
    createdAt: '2026-06-20T12:00:00Z'
  },
  {
    id: 'garment-2',
    userId: 'user-1',
    name: 'Wool blazer',
    category: 'Outerwear',
    bodyZone: 'OuterLayer',
    imageUrl: '/uploads/wool-blazer.png',
    thumbnailUrl: '/uploads/wool-blazer.png',
    tags: ['work'],
    primaryColor: 'brown',
    secondaryColors: [],
    material: 'wool',
    brand: null,
    size: null,
    season: ['fall'],
    weatherMinTemp: null,
    weatherMaxTemp: null,
    occasion: [],
    formalityScore: null,
    warmthScore: null,
    comfortScore: null,
    isFavorite: true,
    isArchived: false,
    lastWornAt: null,
    laundryStatus: 'clean',
    createdAt: '2026-06-20T12:00:00Z'
  }
];

function renderWardrobe() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <WardrobePage />
      </MemoryRouter>
    </QueryClientProvider>
  );
}

describe('WardrobePage', () => {
  beforeEach(() => {
    Object.defineProperty(URL, 'createObjectURL', { configurable: true, value: vi.fn(() => 'blob:preview') });
    Object.defineProperty(URL, 'revokeObjectURL', { configurable: true, value: vi.fn() });
  });

  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it('renders editorial search filters checklist and garment cards', async () => {
    const fetchMock = mockWardrobeFetch();

    renderWardrobe();

    expect(await screen.findByRole('heading', { name: /every piece has/i })).toBeInTheDocument();
    expect(screen.getByLabelText(/search wardrobe/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/category filter/i)).toBeInTheDocument();
    expect(within(screen.getByLabelText(/garment categories/i)).getByRole('button', { name: /outerwear/i })).toBeInTheDocument();
    expect(screen.getByLabelText(/clean photo checklist/i)).toHaveTextContent(/front view/i);
    expect(await screen.findByText(/black silk cami/i)).toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledWith('/api/garments?archived=false&sort=recent', expect.any(Object));
  });

  it('calls the garment list endpoint with active filters', async () => {
    const fetchMock = mockWardrobeFetch();

    renderWardrobe();

    const filters = await screen.findByLabelText(/wardrobe filters/i);
    await screen.findByText(/black silk cami/i);
    await userEvent.type(within(filters).getByLabelText(/search wardrobe/i), 'silk');
    await userEvent.selectOptions(within(filters).getByLabelText(/category filter/i), 'Top');
    await userEvent.selectOptions(within(filters).getByLabelText(/^color$/i), 'black');
    await userEvent.selectOptions(within(filters).getByLabelText(/^season$/i), 'summer');
    await userEvent.click(within(filters).getByLabelText(/favorites/i));

    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledWith(
        '/api/garments?q=silk&category=Top&color=black&season=summer&favorite=true&archived=false&sort=recent',
        expect.any(Object)
      );
    });
  });

  it('filters locally by tags and resets filters', async () => {
    mockWardrobeFetch();

    renderWardrobe();

    const filters = await screen.findByLabelText(/wardrobe filters/i);
    await screen.findByText(/black silk cami/i);
    await userEvent.type(within(filters).getByLabelText(/^tags$/i), 'work');

    expect(screen.queryByText(/black silk cami/i)).not.toBeInTheDocument();
    expect(screen.getByText(/wool blazer/i)).toBeInTheDocument();

    await userEvent.click(within(filters).getByRole('button', { name: /reset/i }));
    expect(await screen.findByText(/black silk cami/i)).toBeInTheDocument();
  });

  it('shows empty examples and reset for filtered empty states', async () => {
    mockWardrobeFetch({ emptyForSearch: true });

    renderWardrobe();

    expect(await screen.findByText(/start with a front-view shirt/i)).toBeInTheDocument();
    await userEvent.type(screen.getByLabelText(/search wardrobe/i), 'does not exist');

    expect(await screen.findByRole('button', { name: /reset filters/i })).toBeInTheDocument();
  });

  it('favorites archives edits duplicates and deletes garments through existing API calls', async () => {
    const fetchMock = mockWardrobeFetch();

    renderWardrobe();

    await userEvent.click(await screen.findByRole('button', { name: /favorite black silk cami/i }));
    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledWith('/api/garments/garment-1', expect.objectContaining({ method: 'PATCH', body: expect.stringContaining('"isFavorite":true') }));
    });

    await userEvent.click(await screen.findByRole('button', { name: /archive black silk cami/i }));
    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledWith('/api/garments/garment-1', expect.objectContaining({ method: 'PATCH', body: expect.stringContaining('"isArchived":true') }));
    });

    await userEvent.click(await screen.findByRole('button', { name: /duplicate black silk cami/i }));
    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledWith('/api/garments', expect.objectContaining({ method: 'POST', body: expect.stringContaining('Black silk cami copy') }));
    });

    await userEvent.click(await screen.findByRole('button', { name: /edit black silk cami/i }));
    await userEvent.clear(await screen.findByLabelText(/^name$/i));
    await userEvent.type(screen.getByLabelText(/^name$/i), 'Black silk camisole');
    await userEvent.click(screen.getByRole('button', { name: /save changes/i }));
    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledWith('/api/garments/garment-1', expect.objectContaining({ method: 'PATCH', body: expect.stringContaining('Black silk camisole') }));
    });

    await userEvent.click(await screen.findByRole('button', { name: /delete black silk cami/i }));
    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledWith('/api/garments/garment-1', expect.objectContaining({ method: 'DELETE' }));
    });
  });

  it('supports bulk upload file input camera input drag drop suggestions warnings and submit all', async () => {
    const fetchMock = mockWardrobeFetch();

    renderWardrobe();

    await screen.findByText(/black silk cami/i);
    const fileInput = screen.getByLabelText(/garment photos/i);
    const cameraInput = screen.getByLabelText(/camera garment photo/i);
    expect(cameraInput).toHaveAttribute('capture', 'environment');

    const shirt = new File(['shirt'], 'cream-linen-shirt.png', { type: 'image/png' });
    const tiny = new File(['x'], 'IMG_0001.png', { type: 'image/png' });
    await userEvent.upload(fileInput, [shirt, tiny]);

    expect(await screen.findByLabelText(/upload queue/i)).toBeInTheDocument();
    expect(screen.getByDisplayValue(/cream linen shirt/i)).toBeInTheDocument();
    expect(screen.getAllByText(/needs better photo/i).length).toBeGreaterThan(0);
    expect(within(screen.getByLabelText(/suggested tags for cream linen shirt/i)).getByRole('button', { name: /linen/i })).toBeInTheDocument();

    const dropZone = screen.getByText(/upload photos/i).closest('label');
    expect(dropZone).not.toBeNull();
    fireEvent.drop(dropZone!, {
      dataTransfer: {
        files: [new File(['coat'], 'brown-wool-blazer.webp', { type: 'image/webp' })]
      }
    });
    expect(await screen.findByDisplayValue(/brown wool blazer/i)).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: /add garments/i }));

    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledWith('/api/uploads/garment-photo', expect.objectContaining({ method: 'POST' }));
      expect(fetchMock).toHaveBeenCalledWith('/api/garments', expect.objectContaining({ method: 'POST', body: expect.stringContaining('Cream linen shirt') }));
      expect(fetchMock).toHaveBeenCalledWith('/api/garments', expect.objectContaining({ method: 'POST', body: expect.stringContaining('Brown wool blazer') }));
    });
  });
});

function mockWardrobeFetch(options: { emptyForSearch?: boolean } = {}) {
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input);
    const method = init?.method ?? 'GET';

    if (url.startsWith('/api/garments') && method === 'GET') {
      if (options.emptyForSearch) {
        return jsonResponse([]);
      }

      return jsonResponse(garmentsResponse);
    }

    if (url.endsWith('/uploads/garment-photo') && method === 'POST') {
      return jsonResponse({ fileName: 'new-garment.png', contentType: 'image/png', length: 128, url: '/uploads/new-garment.png' }, 201);
    }

    if (url.endsWith('/garments') && method === 'POST') {
      return jsonResponse({ ...garmentsResponse[0], id: `created-${Date.now()}` }, 201);
    }

    if (url.includes('/garments/garment-1') && method === 'PATCH') {
      return jsonResponse({ ...garmentsResponse[0], name: 'Black silk camisole' });
    }

    if (url.includes('/garments/garment-1') && method === 'DELETE') {
      return new Response(null, { status: 204 });
    }

    return jsonResponse([]);
  });

  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' }
  });
}
