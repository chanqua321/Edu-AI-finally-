(function () {
    'use strict';

    var Actions = {
        Created: 'Created',
        Updated: 'Updated',
        Deleted: 'Deleted',
        Restored: 'Restored',
        TeacherAssigned: 'TeacherAssigned',
        TeacherUnassigned: 'TeacherUnassigned',
        MaterialsRemoved: 'MaterialsRemoved'
    };

    function escapeHtml(text) {
        if (!text) return '';
        var div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    function pageUrl(templateUrl, id) {
        if (!templateUrl) return '#';
        return String(templateUrl).replace(/(\/)\d+(?=[?#]|$)/, '$1' + id);
    }

    function shouldShowSubject(subject, role, userId) {
        if (!subject) return false;
        if (role === 'Admin') return true;
        if (subject.isActive === false) return false;
        if (role === 'Teacher') return subject.teacherId === userId;
        if (role === 'Student') return (subject.documentCount || 0) > 0;
        return false;
    }

    function getActionMessage(action, subject, role, userId, previousTeacherId) {
        var name = subject && subject.name ? '"' + subject.name + '"' : 'môn học';
        switch (action) {
            case Actions.Created:
                return 'Môn học mới: ' + name + '.';
            case Actions.Restored:
                return 'Môn học ' + name + ' đã được khôi phục.';
            case Actions.Updated:
                return 'Môn học ' + name + ' đã được cập nhật.';
            case Actions.Deleted:
                return 'Môn học ' + name + ' đã bị ẩn.';
            case Actions.TeacherAssigned:
                if (role === 'Teacher' && subject && subject.teacherId === userId)
                    return 'Bạn được phân công môn ' + name + '.';
                return 'Giáo viên phụ trách môn ' + name + ' đã thay đổi.';
            case Actions.TeacherUnassigned:
                if (role === 'Teacher' && previousTeacherId === userId)
                    return 'Bạn đã được gỡ khỏi môn ' + name + '.';
                return 'Môn ' + name + ' chưa có giáo viên phụ trách.';
            case Actions.MaterialsRemoved:
                return 'Môn ' + name + ' không còn tài liệu đã index.';
            default:
                return 'Môn học ' + name + ' đã thay đổi.';
        }
    }

    function updateSubjectDetailsPage(subject) {
        if (!subject) return;
        var title = document.getElementById('subject-page-title');
        var subtitle = document.getElementById('subject-page-subtitle');
        var desc = document.getElementById('subject-description');
        var docCount = document.getElementById('subject-doc-count');
        if (title) title.textContent = subject.name || '';
        if (subtitle) {
            subtitle.textContent = subject.teacherName
                ? 'GV: ' + subject.teacherName
                : 'Chưa gán giáo viên';
        }
        if (desc) desc.textContent = subject.description || '—';
        if (docCount) docCount.textContent = String(subject.documentCount || 0);
    }

    function buildTeacherCell(subject, role) {
        if (role !== 'Admin') {
            return escapeHtml(subject.teacherName || '—');
        }
        if (subject.teacherId && subject.teacherName) {
            return '<span class="badge badge-teacher-assigned">' + escapeHtml(subject.teacherName) + '</span>' +
                '<button type="button" class="badge badge-assign-teacher border-0 js-assign-teacher ms-1" ' +
                'data-subject-id="' + subject.id + '" data-subject-name="' + escapeHtml(subject.name) + '" ' +
                'data-teacher-id="' + escapeHtml(subject.teacherId) + '">Đổi</button>';
        }
        return '<button type="button" class="badge badge-assign-teacher border-0 js-assign-teacher" ' +
            'data-subject-id="' + subject.id + '" data-subject-name="' + escapeHtml(subject.name) + '" ' +
            'data-teacher-id="">Gán giáo viên</button>';
    }

    function buildStatusCell(subject, role) {
        if (role !== 'Admin') return '';
        if (subject.isActive !== false) {
            return '<span class="badge bg-success">Hiển thị</span>';
        }
        return '<span class="badge bg-secondary">Đã ẩn</span>';
    }

    function buildHideRestoreButtons(subject, role) {
        if (role !== 'Admin') return '';
        if (subject.isActive !== false) {
            return '<form method="post" action="?handler=HideSubject" class="d-inline">' +
                '<input type="hidden" name="subjectId" value="' + subject.id + '" />' +
                '<button type="submit" class="btn btn-sm btn-outline-warning" ' +
                'onclick="return confirm(\'Ẩn môn này? Giáo viên và sinh viên sẽ không còn thấy môn.\');">Ẩn môn</button></form>';
        }
        return '<form method="post" action="?handler=RestoreSubject" class="d-inline">' +
            '<input type="hidden" name="subjectId" value="' + subject.id + '" />' +
            '<button type="submit" class="btn btn-sm btn-outline-success">Khôi phục</button></form>';
    }

    function buildActionButtons(subject, role, userId, urls) {
        var id = subject.id;
        var docCount = subject.documentCount || 0;
        var html = docCount > 0
            ? '<a href="' + pageUrl(urls.details, id) + '" class="btn btn-sm btn-outline-primary">Chi tiết</a> '
            : '';

        if (role === 'Admin') {
            html += '<a href="' + pageUrl(urls.edit, id) + '" class="btn btn-sm btn-outline-secondary">Sửa</a> ';
            html += buildHideRestoreButtons(subject, role) + ' ';
        }

        if (role === 'Teacher' && urls.upload && subject.teacherId === userId) {
            html += '<a href="' + urls.upload + '?subjectId=' + id + '" class="btn btn-sm btn-primary">Tải lên</a>';
        }

        if (role === 'Student') {
            if ((subject.documentCount || 0) > 0) {
                html += '<a href="' + urls.materials + '?subjectId=' + id + '" class="btn btn-sm btn-outline-success">Tải về</a> ';
            }
            if (subject.hasMaterials) {
                html += '<a href="' + urls.chatCreate + '?subjectId=' + id + '" class="btn btn-sm btn-primary">Chat</a>';
            }
        }

        return html;
    }

    function buildMaterialsCell(subject, role, urls) {
        var count = subject.documentCount || 0;
        if (count <= 0) return '<span class="text-muted">Chưa có tài liệu</span>';
        return '<a href="' + pageUrl(urls.details, subject.id) + '" class="badge badge-doc badge-doc-link text-decoration-none">' + count + ' tài liệu</a>';
    }

    function buildSubjectRow(subject, role, userId, urls) {
        var teacherId = subject.teacherId || '';
        var tr = document.createElement('tr');
        tr.setAttribute('data-subject-id', subject.id);
        tr.setAttribute('data-teacher-id', teacherId);
        tr.setAttribute('data-has-materials', subject.hasMaterials ? 'true' : 'false');
        tr.setAttribute('data-is-active', subject.isActive !== false ? 'true' : 'false');
        if (role === 'Admin' && subject.isActive === false) {
            tr.classList.add('subject-row-inactive');
        }
        var statusCol = role === 'Admin'
            ? '<td>' + buildStatusCell(subject, role) + '</td>'
            : '';
        tr.innerHTML =
            '<td><strong>' + escapeHtml(subject.name) + '</strong></td>' +
            '<td>' + buildTeacherCell(subject, role) + '</td>' +
            '<td class="text-muted">' + escapeHtml(subject.description || '—') + '</td>' +
            '<td>' + buildMaterialsCell(subject, role, urls) + '</td>' +
            statusCol +
            '<td class="text-nowrap">' + buildActionButtons(subject, role, userId, urls) + '</td>';
        return tr;
    }

    function updateSubjectRow(row, subject, role, userId, urls) {
        row.setAttribute('data-teacher-id', subject.teacherId || '');
        row.setAttribute('data-has-materials', subject.hasMaterials ? 'true' : 'false');
        row.setAttribute('data-is-active', subject.isActive !== false ? 'true' : 'false');
        row.classList.toggle('subject-row-inactive', role === 'Admin' && subject.isActive === false);
        row.children[0].innerHTML = '<strong>' + escapeHtml(subject.name) + '</strong>';
        row.children[1].innerHTML = buildTeacherCell(subject, role);
        row.children[2].textContent = subject.description || '—';
        row.children[3].innerHTML = buildMaterialsCell(subject, role, urls);
        if (role === 'Admin') {
            row.children[4].innerHTML = buildStatusCell(subject, role);
            row.children[5].innerHTML = buildActionButtons(subject, role, userId, urls);
        } else {
            row.children[4].innerHTML = buildActionButtons(subject, role, userId, urls);
        }
    }

    function removeSubjectRows(tbody, subjectId) {
        tbody.querySelectorAll('tr[data-subject-id="' + subjectId + '"]').forEach(function (row) {
            row.remove();
        });
    }

    function toggleEmptyState(panel, emptyAlert, tbody) {
        var hasRows = tbody.querySelectorAll('tr[data-subject-id]').length > 0;
        if (emptyAlert) emptyAlert.classList.toggle('d-none', hasRows);
        if (panel) panel.classList.toggle('d-none', !hasRows);
    }

    function applyAdminStatusFilterIfPresent(tbody) {
        // The Admin page has a status filter (all/active/hidden) implemented in Razor inline JS.
        // That script captures the initial rows list, so realtime updates won't be filtered unless we re-apply here.
        var filterRoot = document.getElementById('subjectStatusFilter');
        if (!filterRoot || !tbody) return;

        var activeBtn = filterRoot.querySelector('.nav-link.active[data-filter]');
        var filter = activeBtn ? activeBtn.getAttribute('data-filter') : 'all';
        if (!filter) filter = 'all';

        var rows = Array.from(tbody.querySelectorAll('tr[data-is-active]'));
        rows.forEach(function (row) {
            var isActive = row.getAttribute('data-is-active') === 'true';
            var show = filter === 'all'
                || (filter === 'active' && isActive)
                || (filter === 'hidden' && !isActive);
            row.classList.toggle('d-none', !show);
        });

        var totalRows = rows.length;
        var visibleRows = rows.filter(function (r) { return !r.classList.contains('d-none'); }).length;
        var emptyEl = document.getElementById('subjects-empty');
        var filterEmptyEl = document.getElementById('subjects-filter-empty');
        var panelEl = document.getElementById('subjects-panel');

        if (emptyEl) emptyEl.classList.toggle('d-none', totalRows > 0);
        if (filterEmptyEl) filterEmptyEl.classList.toggle('d-none', visibleRows > 0 || totalRows === 0);
        if (panelEl) panelEl.classList.toggle('d-none', visibleRows === 0);
    }

    function showToast(message) {
        var toast = document.getElementById('subject-realtime-toast');
        if (!toast) return;
        toast.textContent = message;
        toast.classList.remove('d-none');
        clearTimeout(toast._hideTimer);
        toast._hideTimer = setTimeout(function () {
            toast.classList.add('d-none');
        }, 4500);
    }

    function refreshRealtimeRoot(rootId) {
        var root = document.getElementById(rootId);
        if (!root) return Promise.resolve(false);
        return fetch(window.location.href, {
            credentials: 'same-origin',
            headers: {
                'X-Requested-With': 'XMLHttpRequest',
                'Accept': 'text/html'
            }
        })
            .then(function (res) { return res.ok ? res.text() : Promise.reject(); })
            .then(function (html) {
                var doc = new DOMParser().parseFromString(html, 'text/html');
                var newRoot = doc.getElementById(rootId);
                if (!newRoot) return false;
                root.innerHTML = newRoot.innerHTML;
                return true;
            })
            .catch(function () { return false; });
    }

    window.refreshDocumentsRealtimeRoot = function () {
        return refreshRealtimeRoot('documents-realtime-root');
    };

    function shouldShowSubjectForChat(subject) {
        return !!(subject && subject.hasMaterials);
    }

    function toggleChatSubjectsEmpty(grid, section, emptyEl) {
        var hasCards = grid && grid.querySelectorAll('[data-subject-id]').length > 0;
        if (section) section.classList.toggle('d-none', !hasCards);
        if (emptyEl) emptyEl.classList.toggle('d-none', hasCards);
    }

    function buildChatSubjectCard(subject, urls) {
        return '<a href="' + pageUrl(urls.chatCreate, subject.id) + '" class="dash-card" data-subject-id="' + subject.id + '">' +
            '<div class="dash-card-icon">&#128172;</div>' +
            '<h5>' + escapeHtml(subject.name) + '</h5>' +
            '<p>' + (subject.documentCount || 0) + ' tài liệu sẵn sàng</p></a>';
    }

    function updateChatSubjectsGrid(subject, urls) {
        var grid = document.getElementById('chat-subjects-grid');
        var section = document.getElementById('chat-subjects-section');
        var emptyEl = document.getElementById('chat-subjects-empty');
        if (!grid) return;

        var existing = grid.querySelector('[data-subject-id="' + subject.id + '"]');
        if (!shouldShowSubjectForChat(subject)) {
            if (existing) existing.remove();
            toggleChatSubjectsEmpty(grid, section, emptyEl);
            return;
        }

        var cardHtml = buildChatSubjectCard(subject, urls);
        if (existing) {
            existing.outerHTML = cardHtml;
        } else {
            grid.insertAdjacentHTML('beforeend', cardHtml);
        }
        toggleChatSubjectsEmpty(grid, section, emptyEl);
    }

    function handleChatSubjectsEvent(evt, urls) {
        var action = evt.action;
        var subjectId = evt.subjectId;
        var subject = evt.subject;
        var grid = document.getElementById('chat-subjects-grid');
        if (!grid) return false;

        if (action === Actions.Deleted || action === Actions.MaterialsRemoved) {
            var removed = grid.querySelector('[data-subject-id="' + subjectId + '"]');
            if (removed) removed.remove();
            toggleChatSubjectsEmpty(grid, document.getElementById('chat-subjects-section'), document.getElementById('chat-subjects-empty'));
            showToast(getActionMessage(action, subject, 'Student', '', evt.previousTeacherId));
            return true;
        }

        if (!subject || !shouldShowSubjectForChat(subject)) return false;

        if (action === Actions.Created || action === Actions.Updated || action === Actions.Restored) {
            updateChatSubjectsGrid(subject, urls);
            showToast(getActionMessage(action, subject, 'Student', '', evt.previousTeacherId));
            return true;
        }

        return false;
    }

    function handleDocumentsPageEvent(evt, role, userId, urls, documentsSubjectId) {
        if (!documentsSubjectId || Number(evt.subjectId) !== Number(documentsSubjectId)) return false;

        var action = evt.action;
        var subject = evt.subject;

        if (action === Actions.Deleted) {
            if (role !== 'Admin') {
                showToast('Môn học này đã bị ẩn.');
                setTimeout(function () {
                    window.location.href = urls.index || '/Subjects';
                }, 1200);
                return true;
            }
        }

        if (action === Actions.Updated || action === Actions.MaterialsRemoved ||
            action === Actions.Created || action === Actions.Restored || action === Actions.Deleted) {
            window.refreshDocumentsRealtimeRoot();
            showToast(getActionMessage(action, subject, role, userId, evt.previousTeacherId));
            return true;
        }

        return false;
    }

    function handleSubjectsTableEvent(evt, role, userId, urls, tbody, panel, emptyAlert) {
        var action = evt.action;
        var subjectId = evt.subjectId;
        var subject = evt.subject;
        var previousTeacherId = evt.previousTeacherId;

        if (action === Actions.Deleted) {
            if (role === 'Admin' && subject) {
                var deletedRow = tbody.querySelector('tr[data-subject-id="' + subjectId + '"]:not(.chunk-expand-row)');
                if (deletedRow) {
                    updateSubjectRow(deletedRow, subject, role, userId, urls);
                } else if (shouldShowSubject(subject, role, userId)) {
                    tbody.appendChild(buildSubjectRow(subject, role, userId, urls));
                    toggleEmptyState(panel, emptyAlert, tbody);
                }
                applyAdminStatusFilterIfPresent(tbody);
                showToast(getActionMessage(action, subject, role, userId, previousTeacherId));
                return;
            }
            removeSubjectRows(tbody, subjectId);
            toggleEmptyState(panel, emptyAlert, tbody);
            applyAdminStatusFilterIfPresent(tbody);
            showToast(getActionMessage(action, subject, role, userId, previousTeacherId));
            return;
        }

        if (action === Actions.MaterialsRemoved && role === 'Student') {
            removeSubjectRows(tbody, subjectId);
            toggleEmptyState(panel, emptyAlert, tbody);
            showToast(getActionMessage(action, subject, role, userId, previousTeacherId));
            return;
        }

        if (role === 'Teacher' && (!subject || !shouldShowSubject(subject, role, userId))) {
            var teacherRow = tbody.querySelector('tr[data-subject-id="' + subjectId + '"]:not(.chunk-expand-row)');
            if (teacherRow) {
                removeSubjectRows(tbody, subjectId);
                toggleEmptyState(panel, emptyAlert, tbody);
                applyAdminStatusFilterIfPresent(tbody);
                showToast(getActionMessage(action, subject, role, userId, previousTeacherId));
            }
            return;
        }

        if (!subject || !shouldShowSubject(subject, role, userId)) {
            return;
        }

        var existing = tbody.querySelector('tr[data-subject-id="' + subject.id + '"]:not(.chunk-expand-row)');
        if (existing) {
            updateSubjectRow(existing, subject, role, userId, urls);
        } else {
            tbody.appendChild(buildSubjectRow(subject, role, userId, urls));
            toggleEmptyState(panel, emptyAlert, tbody);
        }
        applyAdminStatusFilterIfPresent(tbody);
        showToast(getActionMessage(action, subject, role, userId, previousTeacherId));
    }

    function isJsonResponse(response) {
        var contentType = response.headers && response.headers.get
            ? (response.headers.get('content-type') || '')
            : '';
        return contentType.indexOf('application/json') >= 0;
    }

    function closeAssignTeacherModal(form) {
        if (!form || !window.bootstrap) return;
        var modalEl = form.closest('.modal');
        if (!modalEl) return;
        var modal = bootstrap.Modal.getInstance(modalEl) || bootstrap.Modal.getOrCreateInstance(modalEl);
        modal.hide();
    }

    function isAssignTeacherAction(action) {
        return action.indexOf('handler=AssignTeacher') >= 0;
    }

    function wireAjaxForms(connection) {
        // Prevent full page reload for Subject CRUD actions.
        document.addEventListener('submit', function (e) {
            var form = e.target;
            if (!form || form.tagName !== 'FORM') return;
            var action = form.getAttribute('action') || '';
            if (action.indexOf('handler=HideSubject') < 0 &&
                action.indexOf('handler=RestoreSubject') < 0 &&
                action.indexOf('handler=AssignTeacher') < 0) {
                return;
            }

            e.preventDefault();

            var submitBtn = form.querySelector('button[type="submit"],input[type="submit"]');
            if (submitBtn) submitBtn.disabled = true;

            var formData = new FormData(form);
            // Realtime-built forms (from JS) don't include antiforgery token.
            // Reuse any existing token on the page to satisfy ASP.NET Core antiforgery.
            if (!formData.has('__RequestVerificationToken')) {
                var tokenEl = document.querySelector('input[name="__RequestVerificationToken"]');
                if (tokenEl && tokenEl.value) {
                    formData.append('__RequestVerificationToken', tokenEl.value);
                }
            }

            fetch(action, {
                method: 'POST',
                headers: {
                    'X-Requested-With': 'XMLHttpRequest',
                    'Accept': 'application/json'
                },
                body: formData,
                credentials: 'same-origin'
            })
                .then(function (res) {
                    if (res.status === 401) {
                        showToast('Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.');
                        throw new Error('unauthorized');
                    }
                    if (res.status === 403) {
                        showToast('Bạn không có quyền thực hiện thao tác này.');
                        throw new Error('forbidden');
                    }

                    // Some endpoints may still return HTML (e.g. redirect/partial) even though the action succeeded.
                    // In that case, rely on SignalR to refresh the UI and just show a generic toast.
                    if (!isJsonResponse(res)) {
                        if (res.ok) {
                            showToast('Đã thực hiện thao tác.');
                            if (isAssignTeacherAction(action)) {
                                closeAssignTeacherModal(form);
                            }
                            return null;
                        }
                        throw new Error('non-json');
                    }

                    return res.json();
                })
                .then(function (data) {
                    if (!data) return;
                    if (data && data.success) {
                        if (data.message) showToast(data.message);
                        if (isAssignTeacherAction(action)) {
                            closeAssignTeacherModal(form);
                        }
                        // No DOM update here; rely on SignalR SubjectChanged (also sent to the current user).
                        return;
                    }
                    showToast((data && data.error) ? data.error : 'Thao tác không thành công.');
                })
                .catch(function () {
                    showToast('Không thể thực hiện thao tác. Vui lòng thử lại.');
                })
                .finally(function () {
                    if (submitBtn) submitBtn.disabled = false;
                });
        }, true);

        // Ensure our own updates arrive even if connection briefly reconnects.
        if (connection && connection.state === 'Connected') {
            // no-op, connection already active
        }
    }

    function handleDetailsPageEvent(evt, role, userId, urls) {
        var action = evt.action;
        var subjectId = evt.subjectId;
        var subject = evt.subject;
        var previousTeacherId = evt.previousTeacherId;
        var currentSubjectId = evt.currentSubjectId;

        if (subjectId !== currentSubjectId) return false;

        if (action === Actions.Deleted) {
            showToast('Môn học này đã bị ẩn.');
            if (role === 'Admin') {
                setTimeout(function () { window.location.reload(); }, 800);
                return true;
            }
            setTimeout(function () {
                window.location.href = urls.index || '/Subjects';
            }, 1200);
            return true;
        }

        if (!shouldShowSubject(subject, role, userId)) {
            showToast('Môn học này không còn khả dụng với bạn.');
            setTimeout(function () {
                window.location.href = urls.index || '/Subjects';
            }, 1200);
            return true;
        }

        updateSubjectDetailsPage(subject);
        showToast(getActionMessage(action, subject, role, userId, previousTeacherId));

        if (action === Actions.Updated || action === Actions.MaterialsRemoved) {
            refreshRealtimeRoot('subject-details-content');
            refreshRealtimeRoot('subject-student-actions');
        }

        return true;
    }

    window.initSubjectRealtime = function (options) {
        if (!options.hubUrl || !window.signalR) return;

        var role = options.role;
        var userId = options.userId || '';
        var urls = options.urls || {};
        var tbody = document.getElementById('subjects-tbody');
        var panel = document.getElementById('subjects-panel');
        var emptyAlert = document.getElementById('subjects-empty');
        var currentSubjectId = options.currentSubjectId || null;
        var documentsSubjectId = options.documentsSubjectId || null;
        var chatGrid = document.getElementById('chat-subjects-grid');

        if (!tbody && !currentSubjectId && !chatGrid && documentsSubjectId == null) return;

        var connection = new signalR.HubConnectionBuilder()
            .withUrl(options.hubUrl)
            .withAutomaticReconnect()
            .build();

        connection.on('SubjectChanged', function (evt) {
            var action = evt.action;
            var subjectId = evt.subjectId;
            var subject = evt.subject;
            var previousTeacherId = evt.previousTeacherId;

            if (currentSubjectId) {
                evt.currentSubjectId = currentSubjectId;
                if (handleDetailsPageEvent(evt, role, userId, urls)) return;
            }

            if (handleDocumentsPageEvent(evt, role, userId, urls, documentsSubjectId)) return;
            if (handleChatSubjectsEvent(evt, urls)) return;

            if (!tbody) return;

            handleSubjectsTableEvent(evt, role, userId, urls, tbody, panel, emptyAlert);
        });

        connection.onreconnected(function () {
            return connection.invoke('JoinSubjectsFeed');
        });

        connection.start()
            .then(function () { return connection.invoke('JoinSubjectsFeed'); })
            .then(function () { wireAjaxForms(connection); })
            .catch(function (err) {
                console.warn('Subject realtime connection failed:', err);
            });
    };
})();
