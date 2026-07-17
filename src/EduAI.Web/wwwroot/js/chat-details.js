(function () {
    const layout = document.querySelector('.chat-layout');
    const box = document.getElementById('chat-box');
    const form = document.getElementById('chat-form');
    const input = document.getElementById('chat-question');
    const sendBtn = document.getElementById('chat-send-btn');
    const statusEl = document.getElementById('chat-status');
    const errorEl = document.getElementById('chat-error');
    const providerSelect = document.getElementById('chat-provider');
    const providerHelp = document.getElementById('chat-provider-help');
    const documentSelect = document.getElementById('chat-document');
    const documentHelp = document.getElementById('chat-document-help');
    if (!layout || !box || !form || !input || !window.signalR) return;

    const sessionId = Number(layout.dataset.sessionId);
    const subjectId = Number(layout.dataset.subjectId);
    const hubUrl = layout.dataset.hubUrl || '/hubs/chat';
    const defaultProvider = layout.dataset.defaultProvider || '';

    function scrollToBottom() {
        const end = document.getElementById('chat-box-end');
        if (end) end.scrollIntoView({ block: 'end' });
        else box.scrollTop = box.scrollHeight;
    }

    function setSendLabel(text) {
        const label = sendBtn.querySelector('.chat-send-label');
        if (label) label.textContent = text;
        else sendBtn.textContent = text;
    }

    function autosizeInput() {
        input.style.height = 'auto';
        input.style.height = Math.min(input.scrollHeight, 140) + 'px';
    }

    function setStatus(text, isError, isThinking) {
        if (!statusEl) return;
        const textNode = statusEl.querySelector('.chat-status-text');
        if (textNode) textNode.textContent = text;
        else statusEl.textContent = text;
        statusEl.classList.toggle('text-danger', !!isError);
        statusEl.classList.toggle('is-thinking', !!isThinking && !isError);
    }

    function showTypingIndicator(providerLabel) {
        removeTypingIndicator();
        removeEmptyState();
        const wrap = document.createElement('div');
        wrap.className = 'chat-message chat-assistant chat-typing';
        wrap.id = 'chat-typing';
        wrap.setAttribute('aria-live', 'polite');

        const avatar = document.createElement('div');
        avatar.className = 'chat-avatar';
        avatar.setAttribute('aria-hidden', 'true');
        avatar.textContent = '🤖';

        const body = document.createElement('div');
        body.className = 'chat-message-body';

        const roleEl = document.createElement('div');
        roleEl.className = 'chat-role';
        roleEl.textContent = providerLabel ? ('EduAI · ' + providerLabel) : 'EduAI';

        const bubble = document.createElement('div');
        bubble.className = 'chat-bubble';

        const dots = document.createElement('div');
        dots.className = 'chat-typing-dots';
        dots.setAttribute('aria-label', 'Đang soạn câu trả lời');
        dots.innerHTML = '<span></span><span></span><span></span>';

        bubble.appendChild(dots);
        body.appendChild(roleEl);
        body.appendChild(bubble);
        wrap.appendChild(avatar);
        wrap.appendChild(body);

        const end = document.getElementById('chat-box-end');
        if (end) box.insertBefore(wrap, end);
        else box.appendChild(wrap);
        scrollToBottom();
    }

    function removeTypingIndicator() {
        const typing = document.getElementById('chat-typing');
        if (typing) typing.remove();
    }

    function setWaitingUi(waiting, providerLabel) {
        form.classList.toggle('is-waiting', !!waiting);
        if (providerSelect) providerSelect.disabled = !!waiting;
        if (documentSelect) documentSelect.disabled = !!waiting;
        if (waiting) {
            const label = providerLabel
                || (providerSelect && providerSelect.options[providerSelect.selectedIndex]
                    ? providerSelect.options[providerSelect.selectedIndex].textContent.split(' - ')[0]
                    : '');
            showTypingIndicator(label);
            setStatus(label ? ('Đang trả lời bằng ' + label + '…') : 'Đang tạo câu trả lời…', false, true);
            setSendLabel('…');
        } else {
            removeTypingIndicator();
            setSendLabel('Gửi');
        }
    }

    function showError(message) {
        if (!errorEl) return;
        errorEl.textContent = message || 'Đã xảy ra lỗi.';
        errorEl.classList.remove('d-none');
    }

    function clearError() {
        if (!errorEl) return;
        errorEl.textContent = '';
        errorEl.classList.add('d-none');
    }

    function updateProviderHelp() {
        if (!providerSelect || !providerHelp) return;
        const option = providerSelect.options[providerSelect.selectedIndex];
        providerHelp.textContent = option ? option.textContent : '';
    }

    function updateDocumentHelp() {
        if (!documentHelp || !documentSelect) return;
        const option = documentSelect.options[documentSelect.selectedIndex];
        const value = documentSelect.value;
        documentHelp.textContent = value
            ? ('Đang lọc: ' + (option ? option.textContent : value))
            : 'Có thể hỏi: “tài liệu chapter 1”';
    }

    function selectedDocumentId() {
        if (!documentSelect || !documentSelect.value) return null;
        const id = Number(documentSelect.value);
        return Number.isFinite(id) && id > 0 ? id : null;
    }

    async function sendQuestion(text, providerId) {
        isSubmitting = true;
        sendBtn.disabled = true;
        const option = providerSelect && providerSelect.options[providerSelect.selectedIndex];
        const label = option ? option.textContent.split(' - ')[0] : '';
        setWaitingUi(true, label);
        await connection.invoke(
            'SendQuestion',
            sessionId,
            subjectId,
            text,
            providerId || null,
            selectedDocumentId());
    }

    function removeEmptyState() {
        const empty = box.querySelector('.chat-empty');
        if (empty) empty.remove();
    }

    function appendMessage(role, content, citations, usedChunks) {
        removeEmptyState();
        const isUser = role === 'user';
        const wrap = document.createElement('div');
        wrap.className = 'chat-message ' + (isUser ? 'chat-user' : 'chat-assistant');

        const avatar = document.createElement('div');
        avatar.className = 'chat-avatar';
        avatar.setAttribute('aria-hidden', 'true');
        avatar.textContent = isUser ? '👤' : '🤖';

        const body = document.createElement('div');
        body.className = 'chat-message-body';

        const roleEl = document.createElement('div');
        roleEl.className = 'chat-role';
        roleEl.textContent = isUser ? 'Bạn' : 'EduAI';

        const bubble = document.createElement('div');
        bubble.className = 'chat-bubble';

        const textEl = document.createElement('div');
        textEl.className = 'chat-text';
        textEl.textContent = content || '';
        bubble.appendChild(textEl);

        if (citations) {
            const citeEl = document.createElement('div');
            citeEl.className = 'chat-citations';
            citeEl.textContent = 'Nguồn: ' + citations;
            bubble.appendChild(citeEl);
        }

        if (Array.isArray(usedChunks) && usedChunks.length > 0) {
            const chunkPanel = document.createElement('div');
            chunkPanel.className = 'chat-used-chunks';
            const title = document.createElement('div');
            title.className = 'small fw-semibold';
            title.textContent = 'Chunk đã dùng (' + usedChunks.length + ')';
            chunkPanel.appendChild(title);

            const list = document.createElement('ul');
            list.className = 'small mb-0 ps-3';
            usedChunks.forEach(function (c) {
                const li = document.createElement('li');
                const score = typeof c.relevanceScore === 'number'
                    ? ' · score ' + c.relevanceScore.toFixed(2)
                    : '';
                li.textContent = (c.documentFileName || 'Document') +
                    ' · Chunk ' + c.chunkIndex + score;
                if (c.preview) {
                    const preview = document.createElement('div');
                    preview.className = 'text-muted';
                    preview.textContent = c.preview;
                    li.appendChild(preview);
                }
                list.appendChild(li);
            });
            chunkPanel.appendChild(list);
            bubble.appendChild(chunkPanel);
        }

        body.appendChild(roleEl);
        body.appendChild(bubble);
        wrap.appendChild(avatar);
        wrap.appendChild(body);

        const typing = document.getElementById('chat-typing');
        const end = document.getElementById('chat-box-end');
        if (typing) box.insertBefore(wrap, typing);
        else if (end) box.insertBefore(wrap, end);
        else box.appendChild(wrap);
        scrollToBottom();
    }

    let isComposing = false;
    let isSubmitting = false;
    let lastSubmittedQuestion = '';

    const connection = new signalR.HubConnectionBuilder()
        .withUrl(hubUrl)
        .withAutomaticReconnect()
        .build();

    connection.on('ReceiveUserMessage', function (msg) {
        lastSubmittedQuestion = msg.content || lastSubmittedQuestion;
        appendMessage('user', msg.content);
    });

    connection.on('ReceiveAssistantMessage', function (msg) {
        removeTypingIndicator();
        appendMessage('assistant', msg.content, msg.citations, msg.usedChunks);
        isSubmitting = false;
        sendBtn.disabled = false;
        setWaitingUi(false);
        updateQuotaUi(msg.quota);
        setStatus(msg.providerName
            ? ('Đã trả lời bằng ' + msg.providerName + (msg.totalTokens != null ? ' · ' + msg.totalTokens + ' token' : ''))
            : (msg.totalTokens != null ? ('Đã trả lời · ' + msg.totalTokens + ' token') : 'Sẵn sàng'));
        input.focus({ preventScroll: true });
    });

    connection.on('ReceiveError', async function (payload) {
        const message = typeof payload === 'string' ? payload : (payload && payload.message) || 'Đã xảy ra lỗi.';
        if (payload && payload.quota) {
            updateQuotaUi(payload.quota);
        }
        isSubmitting = false;
        sendBtn.disabled = false;
        setWaitingUi(false);
        if (payload && payload.fallbackQuota && payload.fallbackProviderId && providerSelect) {
            const shouldSwitch = window.confirm(message + '\n\nBạn có muốn chuyển sang ' + payload.fallbackProviderName + ' để gửi lại ngay không?');
            if (shouldSwitch) {
                providerSelect.value = payload.fallbackProviderId;
                updateQuotaUi(payload.fallbackQuota);
                clearError();
                try {
                    const lastUserText = lastSubmittedQuestion || input.value.trim();
                    if (!lastUserText) {
                        showError('Không tìm thấy câu hỏi trước đó để gửi lại.');
                        setStatus('Lỗi — thử lại', true);
                        return;
                    }
                    await sendQuestion(lastUserText, payload.fallbackProviderId);
                    return;
                } catch (err) {
                    showError(err.message || 'Không thể chuyển sang model dự phòng.');
                    setStatus('Lỗi — thử lại', true);
                    return;
                }
            }
        }
        showError(message);
        setStatus('Lỗi — thử lại', true);
    });

    connection.onreconnecting(function () {
        setStatus('Đang kết nối lại…', true);
    });

    connection.onreconnected(async function () {
        try {
            await connection.invoke('JoinSession', sessionId);
            setStatus('Đã kết nối realtime');
        } catch (e) {
            setStatus('Không thể tham gia phiên chat', true);
        }
    });

    async function start() {
        try {
            await connection.start();
            await connection.invoke('JoinSession', sessionId);
            setStatus('Đã kết nối realtime');
        } catch (e) {
            setStatus('Không kết nối được SignalR', true);
            showError('Không thể kết nối chat realtime. Tải lại trang và thử lại.');
        }
    }

    input.addEventListener('compositionstart', function () { isComposing = true; });
    input.addEventListener('compositionend', function () { isComposing = false; });
    input.addEventListener('input', autosizeInput);

    if (providerSelect) {
        if (defaultProvider) providerSelect.value = defaultProvider;
        providerSelect.addEventListener('change', updateProviderHelp);
        updateProviderHelp();
    }
    if (documentSelect) {
        documentSelect.addEventListener('change', updateDocumentHelp);
        updateDocumentHelp();
    }

    input.addEventListener('keydown', function (e) {
        if (e.key !== 'Enter' || e.shiftKey) return;
        if (e.isComposing || isComposing) return;
        e.preventDefault();
        form.requestSubmit();
    });

    form.addEventListener('submit', async function (e) {
        e.preventDefault();
        clearError();
        const text = input.value.trim();
        if (!text || isSubmitting) return;
        if (connection.state !== signalR.HubConnectionState.Connected) {
            showError('Chưa kết nối realtime. Đang thử kết nối lại…');
            await start();
            return;
        }

        input.value = '';
        autosizeInput();
        lastSubmittedQuestion = text;

        try {
            await sendQuestion(text, providerSelect ? providerSelect.value : null);
        } catch (err) {
            showError(err.message || 'Không thể gửi câu hỏi.');
            isSubmitting = false;
            sendBtn.disabled = false;
            setWaitingUi(false);
            setStatus('Lỗi — thử lại', true);
        }
    });

    scrollToBottom();
    start();
    autosizeInput();
    input.focus({ preventScroll: true });

    function updateQuotaUi(quota) {
        if (!quota || !providerSelect) return;
        for (let i = 0; i < providerSelect.options.length; i++) {
            const option = providerSelect.options[i];
            if (option.value !== quota.providerId) continue;
            option.textContent = quota.providerDisplayName + ' - ' +
                (quota.canUse
                    ? ('Còn ' + quota.remainingCount + '/' + quota.limitCount + ' lượt ' + quota.windowLabel)
                    : quota.message);
            option.disabled = !quota.canUse;
            break;
        }
        if (providerSelect.value === quota.providerId && !quota.canUse) {
            for (let i = 0; i < providerSelect.options.length; i++) {
                if (!providerSelect.options[i].disabled) {
                    providerSelect.selectedIndex = i;
                    break;
                }
            }
        }
        updateProviderHelp();
    }
})();
