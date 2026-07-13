(function () {
    'use strict';

    function getAction(evt) {
        return (evt && (evt.action || evt.Action || '')).toString();
    }

    function forceLogout(evt, options) {
        var action = getAction(evt);
        if (action !== 'Locked' && action !== 'SessionInvalidated') return;

        var logoutUrl = options.logoutUrl || '/Account/Logout?handler=Force&locked=1';
        window.location.replace(logoutUrl);
    }

    window.initUserRealtime = function (options) {
        if (!options.hubUrl) return;

        if (!window.signalR) {
            console.warn('[UserRealtime] SignalR client not loaded.');
            return;
        }

        var connection = new signalR.HubConnectionBuilder()
            .withUrl(options.hubUrl)
            .withAutomaticReconnect()
            .build();

        connection.on('AccountChanged', function (evt) {
            forceLogout(evt, options);
        });

        connection.onreconnected(function () {
            return connection.invoke('JoinUserFeed');
        });

        connection.start()
            .then(function () {
                return connection.invoke('JoinUserFeed');
            })
            .catch(function (err) {
                console.warn('[UserRealtime] Connection failed:', err);
            });
    };
})();
