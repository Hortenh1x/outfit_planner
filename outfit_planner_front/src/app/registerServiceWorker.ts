// Reloads the page once when an already-controlled page is taken over by a newer worker,
// so visitors stuck on an old shell (the v1 cache-first index.html) get fresh HTML. First-time
// visitors have no controller yet, so the takeover after `clients.claim()` must not reload
// them mid-form. Doing this from the page instead of `client.navigate()` inside the worker's
// activate handler avoids the activation deadlock that v2 had.
export async function registerServiceWorker(reloadPage: () => void = () => window.location.reload()) {
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

    const hadController = Boolean(navigator.serviceWorker.controller);
    let reloaded = false;
    navigator.serviceWorker.addEventListener?.('controllerchange', () => {
      if (hadController && !reloaded) {
        reloaded = true;
        reloadPage();
      }
    });

    await navigator.serviceWorker.register('/sw.js');
  } catch (error) {
    console.info('[OutfitPlanner PWA] Service worker maintenance failed', error);
  }
}
