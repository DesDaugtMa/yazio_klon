// wwwroot/service-worker.js
self.addEventListener('install', (event) => {
    console.log('Service Worker: Installiert');
});

self.addEventListener('fetch', (event) => {
    // Hier könnte man Caching-Logik einbauen
});