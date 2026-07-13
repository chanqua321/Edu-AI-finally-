(function () {
    'use strict';

    function getEntityType(evt) {
        return (evt && (evt.entityType || evt.EntityType || '')).toString();
    }

    function getEntityId(evt) {
        var id = evt && (evt.entityId ?? evt.EntityId);
        return id == null ? '' : String(id);
    }

    function matchesEntities(entityType, entities) {
        return entities.length > 0 && entities.indexOf(entityType) >= 0;
    }

    function shouldReload(evt, entities, sessionId) {
        var entityType = getEntityType(evt);
        if (!entityType) return false;

        if (matchesEntities(entityType, entities))
            return true;

        if (sessionId && entityType === 'ChatSession' && getEntityId(evt) === sessionId)
            return true;

        return false;
    }

    window.initAppRealtime = function (options) {
        if (!options.hubUrl || !window.signalR) return;

        var entities = (options.entities || '')
            .split(',')
            .map(function (s) { return s.trim(); })
            .filter(Boolean);
        var sessionId = options.sessionId ? String(options.sessionId) : '';

        if (entities.length === 0 && !sessionId) return;

        var connection = new signalR.HubConnectionBuilder()
            .withUrl(options.hubUrl)
            .withAutomaticReconnect()
            .build();

        connection.on('EntityChanged', function (evt) {
            if (!shouldReload(evt, entities, sessionId)) return;

            if (typeof window.refreshDocumentsRealtimeRoot === 'function') {
                window.refreshDocumentsRealtimeRoot().then(function (ok) {
                    if (ok) return;
                    window.location.reload();
                });
                return;
            }

            window.location.reload();
        });

        connection.onreconnected(function () {
            return connection.invoke('JoinAppFeed');
        });

        connection.start()
            .then(function () { return connection.invoke('JoinAppFeed'); })
            .catch(function (err) {
                console.warn('[AppRealtime] Connection failed:', err);
            });
    };
})();
