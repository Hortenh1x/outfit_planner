import { afterEach, describe, expect, it, vi } from 'vitest';
import fs from 'node:fs';
import path from 'node:path';
import { registerServiceWorker } from './registerServiceWorker';

const frontendRoot = path.resolve(__dirname, '../..');

describe('PWA foundation', () => {
  afterEach(() => {
    vi.unstubAllEnvs();
    vi.unstubAllGlobals();
  });

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

  // Dev builds (vitest runs as dev) must never install the caching worker: the old
  // cache-first shell froze '/' and '/builder' into white pages. They clean up instead.
  it('unregisters existing service workers and drops app caches in dev builds', async () => {
    const unregister = vi.fn().mockResolvedValue(true);
    const getRegistrations = vi.fn().mockResolvedValue([{ unregister }]);
    const register = vi.fn().mockResolvedValue(undefined);
    const cachesDelete = vi.fn().mockResolvedValue(true);
    vi.stubGlobal('navigator', { serviceWorker: { register, getRegistrations } });
    vi.stubGlobal('caches', { keys: vi.fn().mockResolvedValue(['outfit-planner-shell-v1', 'unrelated-cache']), delete: cachesDelete });

    await registerServiceWorker();

    expect(unregister).toHaveBeenCalled();
    expect(cachesDelete).toHaveBeenCalledWith('outfit-planner-shell-v1');
    expect(cachesDelete).not.toHaveBeenCalledWith('unrelated-cache');
    expect(register).not.toHaveBeenCalled();
  });

  it('registers the service worker in production builds when supported', async () => {
    vi.stubEnv('DEV', false);
    const register = vi.fn().mockResolvedValue(undefined);
    vi.stubGlobal('navigator', { serviceWorker: { register } });

    await registerServiceWorker();

    expect(register).toHaveBeenCalledWith('/sw.js');
  });
});
