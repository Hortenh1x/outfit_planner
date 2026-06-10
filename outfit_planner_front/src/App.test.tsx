import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { cleanup, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import App from './App';

function renderApp(initialEntry = '/builder') {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: {
        retry: false
      }
    }
  });

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[initialEntry]}>
        <App />
      </MemoryRouter>
    </QueryClientProvider>
  );
}

describe('BuilderPage', () => {
  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
  });

  it('replaces service status chips with sign in and register actions in the sidebar', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse([]));

    renderApp();

    expect(screen.queryByLabelText(/system status/i)).not.toBeInTheDocument();
    expect(await screen.findByRole('link', { name: /sign in/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /register/i })).toBeInTheDocument();
  });

  it('redirects anonymous private routes to sign in before loading builder data', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
      const url = String(input);

      if (url.endsWith('/auth/me')) {
        return jsonResponse({ error: 'Authentication required' }, 401);
      }

      return jsonResponse([]);
    });

    renderApp('/builder?mode=person');

    expect(await screen.findByRole('heading', { name: /^sign in$/i })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /save outfit/i })).not.toBeInTheDocument();
    expect(fetchMock.mock.calls.some(([url]) => String(url).endsWith('/garments'))).toBe(false);
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

    renderApp();

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

    renderApp();

    await userEvent.click(await screen.findByRole('button', { name: /delete body reference 1/i }));

    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledWith(expect.stringMatching(/\/body-reference-photos\/body-1$/), expect.objectContaining({ method: 'DELETE' }));
    });
  });

  it('does not show the AI try-on consent checkbox in builder controls', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse([]));

    renderApp();

    expect(await screen.findAllByRole('button', { name: /save outfit/i })).not.toHaveLength(0);
    expect(screen.queryByText(/I consent to AI try-on processing/i)).not.toBeInTheDocument();
  });

  it('renders category choices and real animated mode indicators', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse([]));

    const wardrobe = renderApp('/wardrobe');

    const categoryChoices = await screen.findByRole('radiogroup', { name: /garment type/i });
    expect(categoryChoices).toBeInTheDocument();
    expect(within(categoryChoices).getByRole('radio', { name: /dress/i })).toBeInTheDocument();
    expect(within(categoryChoices).getByRole('radio', { name: /shoes/i })).toBeInTheDocument();
    wardrobe.unmount();

    const builder = renderApp('/builder');

    expect(await within(builder.container).findByRole('button', { name: /clothes only/i })).toBeInTheDocument();
    expect(builder.container.querySelector('.mode-toggle .toggle-motion-indicator')).toBeInTheDocument();
  });

  it('deletes garment photos from wardrobe cards', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
      const url = String(input);

      if (url.endsWith('/garments') && (!init || init.method === undefined)) {
        return jsonResponse([
          {
            id: 'garment-1',
            name: 'linen shirt',
            category: 'Top',
            bodyZone: 'Torso',
            imageUrl: 'http://localhost:5000/uploads/garments/linen-shirt.png',
            thumbnailUrl: 'http://localhost:5000/uploads/garments/linen-shirt.png',
            tags: []
          }
        ]);
      }

      if (url.endsWith('/garments/garment-1') && init?.method === 'DELETE') {
        return new Response(null, { status: 204 });
      }

      return jsonResponse([]);
    });

    renderApp('/wardrobe');

    await userEvent.click(await screen.findByRole('button', { name: /delete linen shirt/i }));

    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledWith(expect.stringMatching(/\/garments\/garment-1$/), expect.objectContaining({ method: 'DELETE' }));
    });
  });
});

describe('CalendarPage', () => {
  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
  });

  it('uses a custom clay date picker instead of the native date input', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
      const url = String(input);

      if (url.endsWith('/outfits')) {
        return jsonResponse([
          {
            id: 'outfit-1',
            name: 'Weekend clay',
            items: [],
            createdAt: '2026-06-07T12:00:00Z'
          }
        ]);
      }

      if (url.includes('/schedule?')) {
        return jsonResponse([]);
      }

      return jsonResponse([]);
    });

    const { container } = renderApp('/calendar');

    expect(await screen.findByRole('button', { name: /choose date/i })).toBeInTheDocument();
    expect(container.querySelector('input[type="date"]')).not.toBeInTheDocument();
  });
});

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' }
  });
}
