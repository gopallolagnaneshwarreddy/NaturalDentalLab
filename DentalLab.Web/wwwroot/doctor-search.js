// Keyboard helper for the searchable doctor dropdown (DoctorSearchSelect).
// Stops Enter from submitting the surrounding form and arrows from moving the
// caret, and keeps the highlighted option scrolled into view.
(function () {
    document.addEventListener('keydown', function (e) {
        var input = e.target;
        if (!input || !input.classList) return;

        // Inside a quick-add pop-up, Enter saves the pop-up; it must never
        // submit the page form behind it.
        if (e.key === 'Enter' && input.closest && input.closest('.qa-modal')) {
            e.preventDefault();
            return;
        }

        if (!input.classList.contains('dss-input')) return;

        if (e.key === 'Enter' || e.key === 'ArrowUp' || e.key === 'ArrowDown') {
            e.preventDefault();
        }

        if (e.key === 'ArrowUp' || e.key === 'ArrowDown') {
            setTimeout(function () {
                var wrapper = input.closest('.dss');
                var active = wrapper && wrapper.querySelector('.dss-item.active');
                if (active) active.scrollIntoView({ block: 'nearest' });
            }, 60);
        }
    }, true);
})();

// Backup for browsers without CSS :has(): while a quick-add pop-up is open,
// mark <body> so glass panels drop their blur (the blur would otherwise trap
// the pop-up inside the panel instead of covering the whole screen).
(function () {
    function sync() {
        document.body.classList.toggle('qa-open', !!document.querySelector('.qa-modal'));
    }
    function start() {
        sync();
        new MutationObserver(sync).observe(document.body, { childList: true, subtree: true });
    }
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', start);
    } else {
        start();
    }
})();
