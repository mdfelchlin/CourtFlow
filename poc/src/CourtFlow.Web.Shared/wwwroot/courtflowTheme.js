window.courtflowTheme = {
    setCookie: function (value) {
        const maxAge = 60 * 60 * 24 * 365; // 1 year
        document.cookie = "courtflow-theme=" + encodeURIComponent(value) + "; path=/; max-age=" + maxAge + "; samesite=lax";
    }
};
