import { describe, expect, it, vi } from 'vitest';
import fs from 'node:fs';
import path from 'node:path';
import { registerServiceWorker } from './registerServiceWorker';

const frontendRoot = path.resolve(__dirname, '../..');

describe('PWA foundation', () => {
  it('links a web manifest from index.html', () => {
    const index = fs.readFileSync(path.join(frontendRoot, 'index.html'), 'utf8');

    expect(index).toContain('<link rel="manifest" href="/manifest.webmanifest" />');
    expect(index).toContain('<meta name="theme-color" content="#F4F1FA" />');
  });

  it('defines installable app metadata', () => {
    const manifest = JSON.parse(fs.readFileSync(path.join(frontendRoot, 'public', 'manifest.webmanifest'), 'utf8')) as {
      name: string;
      short_name: string;
      display: string;
      start_url: string;
      icons: Array<{ src: string }>;
    };

    expect(manifest.name).toBe('Outfit Planner');
    expect(manifest.short_name).toBe('Outfits');
    expect(manifest.display).toBe('standalone');
    expect(manifest.start_url).toBe('/builder');
    expect(manifest.icons.some((icon) => icon.src.includes('/icons/outfit-icon.svg'))).toBe(true);
  });

  it('registers the service worker only when supported', async () => {
    const register = vi.fn().mockResolvedValue(undefined);
    vi.stubGlobal('navigator', { serviceWorker: { register } });

    await registerServiceWorker();

    expect(register).toHaveBeenCalledWith('/sw.js');
  });
});
