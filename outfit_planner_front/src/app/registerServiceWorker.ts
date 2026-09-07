export async function registerServiceWorker() {
  if (typeof navigator === 'undefined' || !('serviceWorker' in navigator)) {
    return;
  }

  try {
    // Vite dev (which currently includes the tunnel-fronted public deployment) must not
    // run a caching service worker: the old cache-first shell is what froze '/' and
    // '/builder' into white pages. Unregister leftovers and drop our caches so already
    // broken visitors heal on their next navigation.
    if (import.meta.env.DEV) {
      const registrations = await navigator.serviceWorker.getRegistrations();
      await Promise.all(registrations.map((registration) => registration.unregister()));

      if (typeof caches !== 'undefined') {
        const keys = await caches.keys();
        await Promise.all(keys.filter((key) => key.startsWith('outfit-planner-')).map((key) => caches.delete(key)));
      }

      return;
    }

    await navigator.serviceWorker.register('/sw.js');
  } catch (error) {
    console.info('[OutfitPlanner PWA] Service worker maintenance failed', error);
  }
}
