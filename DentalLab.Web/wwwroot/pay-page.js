// Pay page (/pay/...): UPI app links only work on phones. On a computer, mark
// the card so the page shows the QR code first with a clear explanation.
(function () {
    function mark() {
        var card = document.querySelector('[data-pay-card]');
        if (!card) return;
        var ua = navigator.userAgent || '';
        var isPhone = /Android|iPhone|iPad|iPod|Mobile|Windows Phone/i.test(ua)
            || (navigator.maxTouchPoints > 1 && /Macintosh/.test(ua)); // iPadOS
        card.classList.toggle('is-desktop', !isPhone);
    }
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', mark);
    } else {
        mark();
    }
    // Blazor enhanced navigation swaps page content without a full reload
    document.addEventListener('DOMContentLoaded', function () {
        if (window.Blazor && window.Blazor.addEventListener) {
            window.Blazor.addEventListener('enhancedload', mark);
        }
    });
})();
