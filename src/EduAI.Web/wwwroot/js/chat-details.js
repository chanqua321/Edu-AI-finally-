(function () {
    const box = document.getElementById('chat-box');
    const form = document.getElementById('chat-form');
    const input = document.getElementById('chat-question');
    const sendBtn = document.getElementById('chat-send-btn');
    if (!box || !form || !input) return;

    function scrollToBottom() {
        const end = document.getElementById('chat-box-end');
        if (end) {
            end.scrollIntoView({ block: 'end' });
        } else {
            box.scrollTop = box.scrollHeight;
        }
    }

    scrollToBottom();
    requestAnimationFrame(scrollToBottom);
    window.addEventListener('load', scrollToBottom);

    let isComposing = false;
    let isSubmitting = false;

    input.addEventListener('compositionstart', function () {
        isComposing = true;
    });

    input.addEventListener('compositionend', function () {
        isComposing = false;
    });

    input.addEventListener('keydown', function (e) {
        if (e.key !== 'Enter' || e.shiftKey) return;
        if (e.isComposing || isComposing) return;
        e.preventDefault();
        form.requestSubmit();
    });

    form.addEventListener('submit', function (e) {
        const text = input.value.trim();
        if (!text) {
            e.preventDefault();
            return;
        }
        if (isSubmitting) {
            e.preventDefault();
            return;
        }
        isSubmitting = true;
        sendBtn.disabled = true;
        sendBtn.textContent = 'Đang gửi...';
    });

    input.focus({ preventScroll: true });
    scrollToBottom();
})();
