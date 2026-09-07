// v2: navigations are network-first. v1 precached '/' and '/builder' and served them
// cache-first forever, so visitors kept a frozen index.html and saw a white page
// whenever its JS entry could no longer load. Never precache routed HTML here.
const CACHE_NAME = 'outfit-planner-shell-v2';
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
      // One-time refresh per SW update: windows opened against the stale v1
      // cache-first shell (possibly a white page) reload onto fresh HTML.
      .then(() => self.clients.matchAll({ type: 'window' }))
      .then((clients) => Promise.all(clients.map((client) => client.navigate(client.url).catch(() => null))))
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
