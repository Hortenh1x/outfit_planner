import fs from 'node:fs';
import react from '@vitejs/plugin-react';
import mkcert from 'vite-plugin-mkcert';
import { configDefaults, defineConfig } from 'vitest/config';

export default defineConfig(({ mode }) => {
  const useHttps = process.env.VITE_DEV_HTTPS === 'true' || mode === 'https';
  const devApiTarget = process.env.VITE_DEV_API_TARGET ?? 'https://localhost:5001';
  const httpsOptions = useHttps ? getHttpsOptions() : undefined;
  // Hostnames allowed when the dev server is fronted by a public domain (e.g. a Cloudflare Tunnel).
  // Comma-separated in VITE_ALLOWED_HOSTS. localhost / IPs are always allowed by Vite regardless.
  const allowedHosts = process.env.VITE_ALLOWED_HOSTS
    ? process.env.VITE_ALLOWED_HOSTS.split(',').map((host) => host.trim()).filter(Boolean)
    : undefined;

  return {
    plugins: [
      react(),
      ...(useHttps && !httpsOptions ? [mkcert()] : [])
    ],
    server: {
      https: httpsOptions ?? (useHttps ? {} : undefined),
      allowedHosts,
      proxy: {
        '/api': {
          target: devApiTarget,
          changeOrigin: true,
          secure: false,
          xfwd: true
        },
        '/uploads': {
          target: devApiTarget,
          changeOrigin: true,
          secure: false,
          xfwd: true
        }
      }
    },
    test: {
      exclude: [...configDefaults.exclude, 'e2e/**'],
      environment: 'jsdom',
      setupFiles: './src/test/setup.ts'
    }
  };
});

function getHttpsOptions() {
  const pfxPath = process.env.VITE_DEV_HTTPS_PFX;

  if (!pfxPath || !fs.existsSync(pfxPath)) {
    return undefined;
  }

  return {
    pfx: fs.readFileSync(pfxPath),
    passphrase: process.env.VITE_DEV_HTTPS_PFX_PASSPHRASE
  };
}
