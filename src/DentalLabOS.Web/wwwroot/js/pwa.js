// Registers the service worker (static assets + offline page only; never caches lab data).
if ("serviceWorker" in navigator && (location.protocol === "https:" || location.hostname === "localhost")) {
  addEventListener("load", () => navigator.serviceWorker.register("/sw.js").catch(() => { /* PWA is optional */ }));
}
