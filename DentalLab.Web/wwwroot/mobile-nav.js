/*
 * Mobile navigation drawer.
 *  - Hamburger button ([data-nav-toggle]) opens / closes the sidebar.
 *  - Swipe right from the left screen edge to open, swipe left to close
 *    (the drawer follows the finger while dragging).
 *  - Tap the dark backdrop, the X button ([data-nav-close]), a menu link,
 *    or press Esc to close.
 * State lives on <html class="nav-open"> so Blazor re-renders can't reset it.
 * Everything is delegated on `document`, so it survives enhanced navigation
 * and works with both static SSR and interactive Blazor pages.
 */
(function () {
    'use strict';
    if (window.__dlMobileNav) return;
    window.__dlMobileNav = true;

    var root = document.documentElement;
    var mq = window.matchMedia('(max-width: 640.98px)');
    var EDGE = 28;          // px from left edge where an open-swipe may start
    var LOCK = 10;          // px of movement before we decide the gesture's direction
    var OPEN_CLASS = 'nav-open';

    function sidebar() { return document.getElementById('app-sidebar'); }
    function backdrop() { return document.querySelector('.nav-backdrop'); }
    function isOpen() { return root.classList.contains(OPEN_CLASS); }

    function syncAria() {
        var open = isOpen();
        document.querySelectorAll('[data-nav-toggle]').forEach(function (b) {
            b.setAttribute('aria-expanded', open ? 'true' : 'false');
            b.setAttribute('aria-label', open ? 'Close navigation menu' : 'Open navigation menu');
        });
    }

    function setOpen(open) {
        root.classList.toggle(OPEN_CLASS, open);
        syncAria();
    }

    // ---- Click handling (delegated) ----
    document.addEventListener('click', function (e) {
        if (!mq.matches) return;
        var t = e.target;
        if (!(t instanceof Element)) return;

        if (t.closest('[data-nav-toggle]')) { setOpen(!isOpen()); return; }
        if (t.closest('[data-nav-close]')) { setOpen(false); return; }
        // Any link inside the drawer -> close after navigating
        if (isOpen() && t.closest('#app-sidebar a[href]')) { setOpen(false); }
    });

    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape' && isOpen()) setOpen(false);
    });

    // Reset when rotating / resizing up to desktop width, or after navigation
    mq.addEventListener('change', function () { setOpen(false); clearInline(); });
    document.addEventListener('enhancednavigationend', function () { setOpen(false); });
    window.addEventListener('popstate', function () { setOpen(false); });

    // ---- Swipe / drag ----
    var drag = null;

    function clearInline() {
        var s = sidebar(), b = backdrop();
        if (s) s.style.transform = '';
        if (b) { b.style.opacity = ''; }
        root.classList.remove('nav-dragging');
    }

    function drawerWidth() {
        var s = sidebar();
        return s ? s.getBoundingClientRect().width || 280 : 280;
    }

    function render(x) {                       // x = current visible width of the drawer
        var w = drawerWidth();
        x = Math.max(0, Math.min(w, x));
        var s = sidebar(), b = backdrop();
        if (s) s.style.transform = 'translateX(' + (x - w) + 'px)';
        if (b) b.style.opacity = String(x / w);
        return x;
    }

    // Don't hijack swipes that start inside something horizontally scrollable
    function inHorizontalScroller(el) {
        while (el && el !== document.body) {
            if (el.scrollWidth > el.clientWidth + 1) {
                var ox = getComputedStyle(el).overflowX;
                if (ox === 'auto' || ox === 'scroll') return true;
            }
            el = el.parentElement;
        }
        return false;
    }

    document.addEventListener('touchstart', function (e) {
        if (!mq.matches || e.touches.length !== 1) { drag = null; return; }
        var t = e.touches[0];
        var open = isOpen();
        var canOpen = !open && t.clientX <= EDGE;
        var canClose = open;
        if (!canOpen && !canClose) { drag = null; return; }
        if (canOpen && inHorizontalScroller(e.target) && t.clientX > 12) { drag = null; return; }

        drag = {
            startX: t.clientX, startY: t.clientY,
            startVisible: open ? drawerWidth() : 0,
            visible: open ? drawerWidth() : 0,
            wasOpen: open, active: false, lastX: t.clientX, lastT: e.timeStamp, vx: 0
        };
    }, { passive: true });

    document.addEventListener('touchmove', function (e) {
        if (!drag) return;
        var t = e.touches[0];
        var dx = t.clientX - drag.startX;
        var dy = t.clientY - drag.startY;

        if (!drag.active) {
            if (Math.abs(dx) < LOCK && Math.abs(dy) < LOCK) return;
            // Vertical gesture (scrolling) or wrong direction -> abandon
            if (Math.abs(dy) > Math.abs(dx) || (!drag.wasOpen && dx < 0) || (drag.wasOpen && dx > 0)) {
                drag = null; return;
            }
            drag.active = true;
            root.classList.add('nav-dragging');
        }

        var dt = e.timeStamp - drag.lastT;
        if (dt > 0) drag.vx = (t.clientX - drag.lastX) / dt;   // px per ms
        drag.lastX = t.clientX; drag.lastT = e.timeStamp;

        drag.visible = render(drag.startVisible + dx);
        if (e.cancelable) e.preventDefault();
    }, { passive: false });

    function finish() {
        if (!drag) return;
        var d = drag; drag = null;
        if (!d.active) return;
        var w = drawerWidth();
        var open;
        if (Math.abs(d.vx) > 0.35) open = d.vx > 0;        // quick flick decides
        else open = d.visible > w * 0.5;                    // otherwise position decides
        // Hand control back to CSS so the transition animates the last bit
        var s = sidebar(), b = backdrop();
        root.classList.remove('nav-dragging');
        if (s) s.style.transform = '';
        if (b) b.style.opacity = '';
        setOpen(open);
    }
    document.addEventListener('touchend', finish, { passive: true });
    document.addEventListener('touchcancel', finish, { passive: true });

    syncAria();
})();
