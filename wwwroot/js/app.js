// Initiate GET request (AJAX-supported)
$(document).on('click', '[data-get]', e => {
    e.preventDefault();
    const url = e.target.dataset.get;
    location = url || location;
});

// Initiate POST request (AJAX-supported)
$(document).on('click', '[data-post]', e => {
    e.preventDefault();
    const url = e.target.dataset.post;
    const f = $('<form>').appendTo(document.body)[0];
    f.method = 'post';
    f.action = url || location;
    f.submit();
});

// Trim input
$('[data-trim]').on('change', e => {
    e.target.value = e.target.value.trim();
});

// Auto uppercase
$('[data-upper]').on('input', e => {
    const a = e.target.selectionStart;
    const b = e.target.selectionEnd;
    e.target.value = e.target.value.toUpperCase();
    e.target.setSelectionRange(a, b);
});

// RESET form
$('[type=reset]').on('click', e => {
    e.preventDefault();
    location = location;
});

// Check all checkboxes
$('[data-check]').on('click', e => {
    e.preventDefault();
    const name = e.target.dataset.check;
    $(`[name=${name}]`).prop('checked', true);
});

// Uncheck all checkboxes
$('[data-uncheck]').on('click', e => {
    e.preventDefault();
    const name = e.target.dataset.uncheck;
    $(`[name=${name}]`).prop('checked', false);
});

// Row checkable (AJAX-supported)
$(document).on('click', '[data-checkable]', e => {
    if ($(e.target).is(':input,a')) return;
    
    $(e.currentTarget)
        .find(':checkbox')
        .prop('checked', (i, v) => !v);
});

// Photo preview
$('.upload input').on('change', e => {
    const f = e.target.files[0];
    const img = $(e.target).siblings('img')[0];

    img.dataset.src ??= img.src;

    if (f && f.type.startsWith('image/')) {
        img.onload = e => URL.revokeObjectURL(img.src);
        img.src = URL.createObjectURL(f);
    }
    else {
        img.src = img.dataset.src;
        e.target.value = '';
    }

    // Trigger input validation
    $(e.target).valid();
});

// PWA: offline access and desktop notifications
if ('serviceWorker' in navigator) {
    window.addEventListener('load', () => {
        navigator.serviceWorker.register('/service-worker.js')
            .catch(err => console.warn('Service worker registration failed:', err));
    });
}

function showDesktopNotification(title, body) {
    if (!('Notification' in window) || Notification.permission !== 'granted') return;

    const options = {
        body: body,
        icon: '/images/favicon.png',
        badge: '/images/favicon.png'
    };

    if (navigator.serviceWorker?.ready) {
        navigator.serviceWorker.ready.then(reg => reg.showNotification(title, options));
    }
    else {
        new Notification(title, options);
    }
}

$(document).on('click', '#enableNotifications', async e => {
    e.preventDefault();

    if (!('Notification' in window)) {
        alert('Desktop notifications are not supported in this browser.');
        return;
    }

    const permission = await Notification.requestPermission();
    if (permission === 'granted') {
        $('#enableNotifications').addClass('is-enabled').text('Notifications On');
        showDesktopNotification('Budget Hotel', 'Desktop notifications are enabled.');
    }
});

$(document).ready(() => {
    if ('Notification' in window && Notification.permission === 'granted') {
        $('#enableNotifications').addClass('is-enabled').text('Notifications On');
    }

    const successToast = $('.toast-success .toast-message').first().text().trim();
    if (successToast) {
        showDesktopNotification('Budget Hotel', successToast);
    }
});
