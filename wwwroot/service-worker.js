const CACHE_NAME = 'budget-hotel-v1';

const CORE_ASSETS = [
    '/',
    '/Home/Index',
    '/GuestCatalog/Index',
    '/css/app.css',
    '/css/pager.css',
    '/js/app.js',
    '/js/jquery.min.js',
    '/js/jquery.unobtrusive-ajax.min.js',
    '/js/jquery.validate.min.js',
    '/js/jquery.validate.unobtrusive.min.js',
    '/images/favicon.png',
    '/images/photo.jpg',
    '/images/loader.gif',
    '/manifest.webmanifest'
];

self.addEventListener('install', event => {
    event.waitUntil(
        caches.open(CACHE_NAME)
            .then(cache => cache.addAll(CORE_ASSETS))
            .then(() => self.skipWaiting())
    );
});

self.addEventListener('activate', event => {
    event.waitUntil(
        caches.keys()
            .then(keys => Promise.all(
                keys.filter(key => key !== CACHE_NAME)
                    .map(key => caches.delete(key))
            ))
            .then(() => self.clients.claim())
    );
});

self.addEventListener('fetch', event => {
    if (event.request.method !== 'GET') return;

    event.respondWith(
        fetch(event.request)
            .then(response => {
                const copy = response.clone();
                caches.open(CACHE_NAME).then(cache => cache.put(event.request, copy));
                return response;
            })
            .catch(() => caches.match(event.request)
                .then(cached => cached || caches.match('/')))
    );
});

self.addEventListener('notificationclick', event => {
    event.notification.close();
    event.waitUntil(clients.openWindow('/'));
});
