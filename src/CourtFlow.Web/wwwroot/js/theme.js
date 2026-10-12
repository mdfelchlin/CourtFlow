// Reads and writes the theme cookie and reports the operating system setting.
// It never touches the page's look, so the loading screen stays light.
window.courtflowTheme = (function () {
    const cookieName = "courtflow-theme";

    function readCookie() {
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
        read: function () {
            const systemPrefersDark = typeof window.matchMedia === "function"
                && window.matchMedia("(prefers-color-scheme: dark)").matches;
            return { cookie: readCookie(), systemPrefersDark: systemPrefersDark };
        },
        write: function (mode) {
            document.cookie = cookieName + "=" + encodeURIComponent(mode) + "; path=/; max-age=31536000; SameSite=Lax";
        }
    };
})();
