// Reads and writes a theme cookie and reports the operating system setting. The caller names the
// cookie, so the public frame and the director frame remember separate choices.
// It never touches the page's look, so the loading screen stays light.
window.courtflowTheme = (function () {
    function readCookie(cookieName) {
        const prefix = cookieName + "=";
        const parts = document.cookie ? document.cookie.split(";") : [];
        for (const part of parts) {
            const trimmed = part.trim();
            if (trimmed.startsWith(prefix)) {
                return decodeURIComponent(trimmed.substring(prefix.length));
            }
        }
        return null;
    }

    return {
        read: function (cookieName) {
            const systemPrefersDark = typeof window.matchMedia === "function"
                && window.matchMedia("(prefers-color-scheme: dark)").matches;
            return { cookie: readCookie(cookieName), systemPrefersDark: systemPrefersDark };
        },
        write: function (cookieName, mode) {
            document.cookie = cookieName + "=" + encodeURIComponent(mode) + "; path=/; max-age=31536000; SameSite=Lax";
        }
    };
})();
