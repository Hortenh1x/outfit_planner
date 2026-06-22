import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { cleanup, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { CalendarPage } from './CalendarPage';

function renderCalendar() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <CalendarPage />
      </MemoryRouter>
    </QueryClientProvider>
  );
}

describe('CalendarPage', () => {
  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
  });

  it('uses an editorial date picker instead of the native date input', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
      const url = String(input);

      if (url.endsWith('/outfits')) {
        return jsonResponse([
          {
            id: 'outfit-1',
            name: 'Weekend look',
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

    const { container } = renderCalendar();

    expect(await screen.findByRole('button', { name: /choose date/i })).toBeInTheDocument();
    expect(container.querySelector('input[type="date"]')).not.toBeInTheDocument();
  });

  it('uses the editorial calendar surface without legacy clay classes', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse([]));

    const { container } = renderCalendar();

    expect(await screen.findByRole('heading', { name: /plan your looks, every day/i })).toBeInTheDocument();
    expect(container.querySelector('.calendar-editorial-page')).toBeInTheDocument();
    expect(container.querySelector('.clay-button, .tool-panel, .page-grid, .clay-date-picker')).not.toBeInTheDocument();
  });
});

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' }
  });
}
