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

  it('renders editorial search filters and garment cards', async () => {
    const fetchMock = mockWardrobeFetch();

    renderWardrobe();

    expect(await screen.findByRole('heading', { name: /my wardrobe/i })).toBeInTheDocument();
    expect(screen.getByLabelText(/search wardrobe/i)).toBeInTheDocument();
    expect(screen.queryByLabelText(/category filter/i)).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/^archived$/i)).not.toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: /^tags$/i })).toBeInTheDocument();
    expect(within(screen.getByLabelText(/garment categories/i)).getByRole('button', { name: /outerwear/i })).toBeInTheDocument();
    expect(await screen.findByRole('img', { name: /black silk cami/i })).toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledWith('/api/garments?archived=false&sort=recent', expect.any(Object));
  });

  it('calls the garment list endpoint with active filters', async () => {
    const fetchMock = mockWardrobeFetch();

    renderWardrobe();

    const filters = await screen.findByLabelText(/wardrobe filters/i);
    await screen.findByRole('img', { name: /black silk cami/i });
    await userEvent.type(within(filters).getByLabelText(/search wardrobe/i), 'silk');
    await userEvent.click(within(screen.getByLabelText(/garment categories/i)).getByRole('button', { name: /^top$/i }));
    await userEvent.selectOptions(within(filters).getByLabelText(/^color$/i), 'black');
    await userEvent.selectOptions(within(filters).getByLabelText(/^season$/i), 'summer');

    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledWith(
        '/api/garments?q=silk&category=Top&color=black&season=summer&archived=false&sort=recent',
        expect.any(Object)
      );
    });
  });

  it('filters locally by tags and resets filters', async () => {
    mockWardrobeFetch();

    renderWardrobe();

    const filters = await screen.findByLabelText(/wardrobe filters/i);
    await screen.findByRole('img', { name: /black silk cami/i });
    const tagsCombobox = within(filters).getByRole('combobox', { name: /^tags$/i });
    await userEvent.click(tagsCombobox);

    const suggestions = await screen.findByRole('listbox', { name: /tag suggestions/i });
    expect(within(suggestions).getByRole('option', { name: 'silk' })).toBeInTheDocument();
    expect(within(suggestions).getByRole('option', { name: 'evening' })).toBeInTheDocument();
    expect(within(suggestions).getByRole('option', { name: 'work' })).toBeInTheDocument();

    await userEvent.type(tagsCombobox, 'wor');
    expect(within(suggestions).queryByRole('option', { name: 'silk' })).not.toBeInTheDocument();
    await userEvent.click(within(suggestions).getByRole('option', { name: 'work' }));

    expect(screen.queryByRole('img', { name: /black silk cami/i })).not.toBeInTheDocument();
    expect(screen.getByRole('img', { name: /wool blazer/i })).toBeInTheDocument();

    await userEvent.click(within(filters).getByRole('button', { name: /reset/i }));
    expect(await screen.findByRole('img', { name: /black silk cami/i })).toBeInTheDocument();
  });

  it('shows empty examples and reset for filtered empty states', async () => {
    mockWardrobeFetch({ emptyForSearch: true });

    renderWardrobe();

    expect(await screen.findByText(/start with a front-view shirt/i)).toBeInTheDocument();
    await userEvent.type(screen.getByLabelText(/search wardrobe/i), 'does not exist');

    expect(await screen.findByRole('button', { name: /reset filters/i })).toBeInTheDocument();
  });

  it('edits and deletes garments through existing API calls', async () => {
    const fetchMock = mockWardrobeFetch();

    renderWardrobe();

    // The like and duplicate actions were removed from cards; favorites is no longer a filter.
    expect(screen.queryByRole('button', { name: /favorite black silk cami/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /duplicate black silk cami/i })).not.toBeInTheDocument();

    await userEvent.click(await screen.findByRole('button', { name: /edit black silk cami/i }));
    await userEvent.clear(await screen.findByLabelText(/^name$/i));
    await userEvent.type(screen.getByLabelText(/^name$/i), 'Black silk camisole');
    await userEvent.click(screen.getByRole('button', { name: /save changes/i }));
    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledWith('/api/garments/garment-1', expect.objectContaining({ method: 'PATCH', body: expect.stringContaining('Black silk camisole') }));
    });

    vi.spyOn(window, 'confirm').mockReturnValue(true);
    await userEvent.click(await screen.findByRole('button', { name: /delete black silk cami/i }));
    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledWith('/api/garments/garment-1', expect.objectContaining({ method: 'DELETE' }));
    });
  });

  it('does not delete a garment when the confirmation is dismissed', async () => {
    const fetchMock = mockWardrobeFetch();
    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(false);

    renderWardrobe();

    await userEvent.click(await screen.findByRole('button', { name: /delete black silk cami/i }));

    expect(confirmSpy).toHaveBeenCalled();
    expect(fetchMock).not.toHaveBeenCalledWith('/api/garments/garment-1', expect.objectContaining({ method: 'DELETE' }));
  });

  it('supports bulk upload file input camera input drag drop suggestions warnings and submit all', async () => {
    const fetchMock = mockWardrobeFetch();

    renderWardrobe();

    await screen.findByRole('img', { name: /black silk cami/i });
    const fileInput = screen.getByLabelText(/garment photos/i);
    const cameraInput = screen.getByLabelText(/camera garment photo/i);
    expect(cameraInput).toHaveAttribute('capture', 'environment');

    const shirt = new File(['shirt'], 'cream-linen-shirt.png', { type: 'image/png' });
    const tiny = new File(['x'], 'IMG_0001.png', { type: 'image/png' });
    await userEvent.upload(fileInput, [shirt, tiny]);

    expect(await screen.findByLabelText(/upload queue/i)).toBeInTheDocument();
    // Background removal starts on selection: the preview swaps to the processed cutout.
    await waitFor(() => {
      expect(screen.getByRole('img', { name: /preview of cream-linen-shirt\.png/i })).toHaveAttribute(
        'src',
        '/uploads/new-garment-cutout.png'
      );
    });
    expect(fetchMock).toHaveBeenCalledWith('/api/uploads/garment-original', expect.objectContaining({ method: 'POST' }));
    expect(screen.getByDisplayValue(/cream linen shirt/i)).toBeInTheDocument();
    expect(screen.getAllByText(/needs better photo/i).length).toBeGreaterThan(0);
    expect(within(screen.getByLabelText(/tags for cream linen shirt/i)).getByText(/linen/i)).toBeInTheDocument();

    const dropZone = screen.getByText(/upload photos/i).closest('label');
    expect(dropZone).not.toBeNull();
    fireEvent.drop(dropZone!, {
      dataTransfer: {
        files: [new File(['coat'], 'brown-wool-blazer.webp', { type: 'image/webp' })]
      }
    });
    expect(await screen.findByDisplayValue(/brown wool blazer/i)).toBeInTheDocument();

    // Wait until eager background removal settles, then submit only creates garments.
    const submitButton = await screen.findByRole('button', { name: /add garments/i });
    await waitFor(() => expect(submitButton).toBeEnabled());
    await userEvent.click(submitButton);

    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledWith('/api/garments', expect.objectContaining({ method: 'POST', body: expect.stringContaining('Cream linen shirt') }));
      expect(fetchMock).toHaveBeenCalledWith('/api/garments', expect.objectContaining({ method: 'POST', body: expect.stringContaining('Brown wool blazer') }));
    });
  });

  it('never shows the shared upload defaults block and hides the empty-queue note', async () => {
    mockWardrobeFetch();

    renderWardrobe();

    await screen.findByRole('img', { name: /black silk cami/i });
    // The Type/Color/Season/Tags defaults block and the empty hint are gone.
    expect(screen.queryByLabelText(/upload defaults/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/build an upload queue/i)).not.toBeInTheDocument();
    // The drop zone and camera input are still available to start a queue.
    expect(screen.getByLabelText(/garment photos/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/camera garment photo/i)).toBeInTheDocument();

    await userEvent.upload(
      screen.getByLabelText(/garment photos/i),
      new File(['shirt'], 'plain-shirt.png', { type: 'image/png' })
    );

    // The per-upload queue row appears, but the shared defaults block still does not.
    expect(await screen.findByLabelText(/upload queue/i)).toBeInTheDocument();
    expect(screen.queryByLabelText(/upload defaults/i)).not.toBeInTheDocument();
  });

  it('flags a photo that duplicates an existing wardrobe garment and blocks submit', async () => {
    const hash = 'ffffffffffffffff';
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      const method = init?.method ?? 'GET';

      if (url.startsWith('/api/garments') && method === 'GET') {
        return jsonResponse([{ ...garmentsResponse[0], perceptualHash: hash }]);
      }

      if ((url.endsWith('/uploads/garment-photo') || url.endsWith('/uploads/garment-original')) && method === 'POST') {
        return jsonResponse({
          fileName: 'dup.png',
          contentType: 'image/png',
          length: 128,
          url: '/uploads/dup.png',
          thumbnailUrl: '/uploads/dup-thumb.png',
          cutoutUrl: '/uploads/dup-cutout.png',
          perceptualHash: hash
        }, 201);
      }

      return jsonResponse([]);
    });
    vi.stubGlobal('fetch', fetchMock);

    renderWardrobe();

    await screen.findByRole('img', { name: /black silk cami/i });
    await userEvent.upload(
      screen.getByLabelText(/garment photos/i),
      new File(['dup'], 'dup.png', { type: 'image/png' })
    );

    // Its pre-removal hash matches an existing garment, so the row is flagged and excluded.
    expect(await screen.findByText(/already in your wardrobe/i)).toBeInTheDocument();
    await waitFor(() => expect(screen.getByRole('button', { name: /add garments/i })).toBeDisabled());
  });

  it('updates upload queue tag chips while editing row fields', async () => {
    mockWardrobeFetch();

    renderWardrobe();

    await screen.findByRole('img', { name: /black silk cami/i });
    await userEvent.upload(
      screen.getByLabelText(/garment photos/i),
      new File(['shirt'], 'plain-shirt.png', { type: 'image/png' })
    );

    const uploadQueue = await screen.findByLabelText(/upload queue/i);
    const nameInput = within(uploadQueue).getByLabelText(/^name$/i);
    await userEvent.clear(nameInput);
    await userEvent.type(nameInput, 'Ruby office jacket');

    const rowTags = await screen.findByLabelText(/tags for ruby office jacket/i);
    expect(within(rowTags).getByText('ruby')).toBeInTheDocument();
    expect(within(rowTags).getByText('office')).toBeInTheDocument();
    expect(within(rowTags).getByText('jacket')).toBeInTheDocument();
    expect(within(rowTags).queryByText('plain')).not.toBeInTheDocument();
    expect(within(rowTags).queryByText('shirt')).not.toBeInTheDocument();

    await userEvent.clear(nameInput);
    expect(within(rowTags).queryByText('ruby')).not.toBeInTheDocument();
    expect(within(rowTags).queryByText('office')).not.toBeInTheDocument();
    expect(within(rowTags).queryByText('jacket')).not.toBeInTheDocument();
    expect(within(rowTags).getByText('top')).toBeInTheDocument();

    await userEvent.type(nameInput, 'Ruby office jacket');

    await userEvent.clear(within(uploadQueue).getByLabelText(/^color$/i));
    await userEvent.type(within(uploadQueue).getByLabelText(/^color$/i), 'rose');
    expect(within(rowTags).getByText('rose')).toBeInTheDocument();

    await userEvent.clear(within(uploadQueue).getByLabelText(/^season$/i));
    await userEvent.type(within(uploadQueue).getByLabelText(/^season$/i), 'winter');
    expect(within(rowTags).getByText('winter')).toBeInTheDocument();

    await userEvent.selectOptions(within(uploadQueue).getByLabelText(/^type$/i), 'Bottom');
    expect(within(rowTags).getByText('bottom')).toBeInTheDocument();
    expect(within(rowTags).queryByText('top')).not.toBeInTheDocument();

    // Adding a tag through the chip editor freezes auto-suggestion (tagsEdited = true).
    await userEvent.type(within(uploadQueue).getByLabelText('Add tag'), 'formal{enter}');
    expect(within(rowTags).getByText('formal')).toBeInTheDocument();

    // Frozen tags are no longer rewritten when other row fields change.
    await userEvent.clear(within(uploadQueue).getByLabelText(/^color$/i));
    await userEvent.type(within(uploadQueue).getByLabelText(/^color$/i), 'cobalt');
    expect(within(rowTags).queryByText('cobalt')).not.toBeInTheDocument();
    expect(within(rowTags).getByText('formal')).toBeInTheDocument();

    // The trash control removes just that tag.
    await userEvent.click(within(rowTags).getByRole('button', { name: /remove tag formal/i }));
    expect(within(rowTags).queryByText('formal')).not.toBeInTheDocument();
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

    if ((url.endsWith('/uploads/garment-photo') || url.endsWith('/uploads/garment-original')) && method === 'POST') {
      return jsonResponse({
        fileName: 'new-garment.png',
        contentType: 'image/png',
        length: 128,
        url: '/uploads/new-garment.png',
        thumbnailUrl: '/uploads/new-garment-thumb.png',
        cutoutUrl: '/uploads/new-garment-cutout.png'
      }, 201);
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
