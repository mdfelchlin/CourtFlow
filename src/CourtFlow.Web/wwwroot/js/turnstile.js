// Renders Cloudflare Turnstile and hands its token to Blazor. Cloudflare's script loads on first use only.
window.courtflowTurnstile = (() => {
    let loading;

    function load() {
        if (window.turnstile) return Promise.resolve();
        loading ??= new Promise((resolve, reject) => {
            const script = document.createElement("script");
            script.src = "https://challenges.cloudflare.com/turnstile/v0/api.js?render=explicit";
            script.async = true;
            script.onload = () => resolve();
            script.onerror = () => { loading = undefined; reject(new Error("Turnstile failed to load")); };
            document.head.appendChild(script);
        });
        return loading;
    }

    return {
        async render(element, siteKey, theme, dotNet) {
            await load();
            return window.turnstile.render(element, {
                sitekey: siteKey,
                theme,
                callback: token => dotNet.invokeMethodAsync("OnToken", token),
                "expired-callback": () => dotNet.invokeMethodAsync("OnToken", null),
                "error-callback": () => dotNet.invokeMethodAsync("OnToken", null),
            });
        },
        reset(widgetId) {
            if (window.turnstile && widgetId) window.turnstile.reset(widgetId);
        },
        remove(widgetId) {
            if (window.turnstile && widgetId) window.turnstile.remove(widgetId);
        },
    };
})();
