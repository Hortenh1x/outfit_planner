import react from '@vitejs/plugin-react';
import mkcert from 'vite-plugin-mkcert';
import { defineConfig } from 'vitest/config';

export default defineConfig(({ mode }) => {
  const useHttps = process.env.VITE_DEV_HTTPS === 'true' || mode === 'https';
  const devApiTarget = process.env.VITE_DEV_API_TARGET ?? 'http://localhost:5000';

  return {
    plugins: [
      react(),
      ...(useHttps ? [mkcert()] : [])
    ],
    server: {
      https: useHttps ? {} : undefined,
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
      environment: 'jsdom',
      setupFiles: './src/test/setup.ts'
    }
  };
});
