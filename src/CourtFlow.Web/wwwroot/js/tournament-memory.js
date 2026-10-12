// Remembers the last tournament picked on the Teams page or opened in the editor.
// Storage can be unavailable (private windows, blocked storage); the app then just forgets.
window.courtflowTournamentMemory = (function () {
    const key = "courtflow-tournament";

    return {
        read: function () {
            try {
                return window.localStorage.getItem(key);
            } catch {
                return null;
            }
        },
        write: function (id) {
            try {
                window.localStorage.setItem(key, id);
            } catch {
            }
        },
        clear: function () {
            try {
                window.localStorage.removeItem(key);
            } catch {
            }
        }
    };
})();
