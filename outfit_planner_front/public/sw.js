// v3: navigations are network-first (v1 precached '/' and '/builder' cache-first forever and
// froze visitors on a stale index.html). Never precache routed HTML here.
//
// v2 tried to "heal" v1 visitors by navigating every WindowClient from inside the activate
// waitUntil. That deadlocks in Chromium: the navigation's fetch event waits for activation
// to finish, activation waits for the navigation, and the tab spins for five minutes before
// the browser gives up and reloads the page by itself. The reload-once-after-update now
// lives in the page (registerServiceWorker.ts, on `controllerchange`), never in the worker.
const CACHE_NAME = 'outfit-planner-shell-v3';
const SHELL_ASSETS = ['/offline.html', '/manifest.webmanifest', '/icons/outfit-icon.svg'];

self.addEventListener('install', (event) => {
  event.waitUntil(
    caches.open(CACHE_NAME).then((cache) => cache.addAll(SHELL_ASSETS))
  );
  self.skipWaiting();
});

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys()
      .then((keys) => Promise.all(
        keys.filter((key) => key !== CACHE_NAME).map((key) => caches.delete(key))
      ))
      .then(() => self.clients.claim())
  );
});

self.addEventListener('fetch', (event) => {
  if (event.request.method !== 'GET') {
    return;
  }

  const requestUrl = new URL(event.request.url);
  if (requestUrl.pathname.startsWith('/api/') || requestUrl.pathname.startsWith('/uploads/')) {
    return;
  }

  if (event.request.mode === 'navigate') {
    event.respondWith(
      fetch(event.request)
        .then((response) => {
          if (response.ok) {
            const copy = response.clone();
            caches.open(CACHE_NAME).then((cache) => cache.put(event.request, copy)).catch(() => null);
          }

          return response;
        })
        .catch(async () => (await caches.match(event.request)) ?? (await caches.match('/offline.html')) ?? Response.error())
    );
    return;
  }

  event.respondWith(
    caches.match(event.request).then((cached) => cached ?? fetch(event.request).catch(() => Response.error()))
  );
});
