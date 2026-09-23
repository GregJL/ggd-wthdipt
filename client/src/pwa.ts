let currentRegistration: ServiceWorkerRegistration | null = null;
let updateRequested = false;

export function registerPwa(): void {
  if (!('serviceWorker' in navigator)) return;
  window.addEventListener('load', async () => {
    try {
      const registration = await navigator.serviceWorker.register('/sw.js');
      currentRegistration = registration;
      if (registration.waiting && navigator.serviceWorker.controller)
        window.dispatchEvent(new Event('ggd-pwa-update'));
      registration.addEventListener('updatefound', () => {
        const worker = registration.installing;
        worker?.addEventListener('statechange', () => {
          if (worker.state === 'installed' && navigator.serviceWorker.controller)
            window.dispatchEvent(new Event('ggd-pwa-update'));
        });
      });
      navigator.serviceWorker.addEventListener('controllerchange', () => { if (updateRequested) window.location.reload(); });
    } catch (error) { console.warn('PWA service worker registration failed.', error); }
  });
}

export function applyPwaUpdate(): void {
  updateRequested = true;
  currentRegistration?.waiting?.postMessage({ type: 'SKIP_WAITING' });
}
