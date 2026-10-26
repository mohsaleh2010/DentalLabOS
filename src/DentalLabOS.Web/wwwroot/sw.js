// DentalLab OS service worker.
// Privacy rule (plan: PWA): only static, non-personal assets are cached. HTML pages and API
// responses always go to the network; when offline, navigations fall back to /offline.
const VERSION = "dlos-v1";
const PRECACHE = [
  "/offline",
  "/img/icon.svg",
  "/_content/DentalLabOS.SharedUI/css/dlos.css",
  "/_content/DentalLabOS.SharedUI/js/dlos.js",
  "/_content/DentalLabOS.SharedUI/icons/sprite.svg",
];

self.addEventListener("install", (e) => {
  e.waitUntil(caches.open(VERSION).then((c) => c.addAll(PRECACHE)).then(() => self.skipWaiting()));
});

self.addEventListener("activate", (e) => {
  e.waitUntil(
    caches.keys()
      .then((keys) => Promise.all(keys.filter((k) => k !== VERSION).map((k) => caches.delete(k))))
      .then(() => self.clients.claim()));
});

const isStatic = (url) =>
  url.origin === location.origin &&
  (url.pathname.startsWith("/_content/") || url.pathname.startsWith("/img/") || url.pathname.startsWith("/js/"));

self.addEventListener("fetch", (e) => {
  const req = e.request;
  if (req.method !== "GET") return;
  const url = new URL(req.url);

  if (req.mode === "navigate") {
    e.respondWith(fetch(req).catch(() => caches.match("/offline")));
    return;
  }
  if (isStatic(url)) {
    e.respondWith(
      caches.match(req).then((hit) => hit || fetch(req).then((res) => {
        if (res.ok) { const copy = res.clone(); caches.open(VERSION).then((c) => c.put(req, copy)); }
        return res;
      })));
  }
});
