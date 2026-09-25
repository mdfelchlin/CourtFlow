window.courtflowClerk = (() => {
  let clerkLoaded = false;
  let clerkUiLoaded = false;
  let publishableKey = null;
  let clerkScriptLoadPromise = null;
  let configPromise = null;

  async function getClientConfig() {
    const injected = window.__courtflowClerkConfig;
    if (injected && (injected.publishableKey || injected.frontendApi)) {
      return {
        publishableKey: injected.publishableKey ?? "",
        frontendApi: injected.frontendApi ?? "",
        jwtTemplate: injected.jwtTemplate ?? ""
      };
    }

    if (!configPromise) {
      configPromise = fetch("/appsettings.json", { cache: "no-store" })
        .then(async response => {
          if (!response.ok) {
            throw new Error(`Unable to load /appsettings.json (${response.status})`);
          }

          const json = await response.json();
          const clerk = json?.Clerk ?? {};
          return {
            publishableKey: clerk.PublishableKey ?? "",
            frontendApi: clerk.FrontendApi ?? "",
            jwtTemplate: clerk.JwtTemplate ?? ""
          };
        })
        .catch(() => ({ publishableKey: "", frontendApi: "", jwtTemplate: "" }));
    }

    return configPromise;
  }

  async function loadScript(src) {
    return new Promise((resolve, reject) => {
      const existing = document.querySelector(`script[data-courtflow-clerk="${src}"]`);
      if (existing) {
        if (existing.dataset.loaded === "true") {
          resolve();
          return;
        }

        existing.addEventListener("load", () => resolve(), { once: true });
        existing.addEventListener("error", () => reject(new Error(`Failed to load ${src}`)), { once: true });
        return;
      }

      const script = document.createElement("script");
      script.src = src;
      script.async = true;
      script.crossOrigin = "anonymous";
      script.dataset.courtflowClerk = src;
      script.addEventListener("load", () => {
        script.dataset.loaded = "true";
        resolve();
      }, { once: true });
      script.addEventListener("error", () => reject(new Error(`Failed to load ${src}`)), { once: true });
      document.head.appendChild(script);
    });
  }

  function getClerkDomainFromPublishableKey(key) {
    // Clerk key format: pk_test_<base64-domain-with-trailing-$>
    const parts = (key || "").split("_");
    if (parts.length < 3) return null;

    try {
      const decoded = atob(parts[2]);
      return decoded.endsWith("$") ? decoded.slice(0, -1) : decoded;
    } catch {
      return null;
    }
  }

  async function ensureClerkScript() {
    if (typeof window.Clerk !== "undefined") {
      return;
    }

    if (!clerkScriptLoadPromise) {
      clerkScriptLoadPromise = (async () => {
        const sources = [
          "https://cdn.jsdelivr.net/npm/@clerk/clerk-js@latest/dist/clerk.browser.js",
          "https://unpkg.com/@clerk/clerk-js@latest/dist/clerk.browser.js"
        ];

        let lastError = null;
        for (const src of sources) {
          try {
            await loadScript(src);
            if (typeof window.Clerk !== "undefined") {
              return;
            }
          } catch (err) {
            lastError = err;
          }
        }

        throw lastError ?? new Error("Clerk script not loaded");
      })();
    }

    await clerkScriptLoadPromise;
  }

  async function ensureLoaded(key, frontendApi) {
    await ensureClerkScript();

    const config = await getClientConfig();
    const resolvedKey = key || config.publishableKey || "";
    const resolvedFrontendApi = frontendApi || config.frontendApi || "";

    if (!resolvedKey) {
      throw new Error("Clerk publishable key is required. Checked JS arguments and /appsettings.json Clerk.PublishableKey.");
    }

    if (!clerkUiLoaded) {
      const domain = resolvedFrontendApi
        ? resolvedFrontendApi.replace(/^https?:\/\//, "").replace(/\/+$/, "")
        : getClerkDomainFromPublishableKey(resolvedKey);

      if (!domain) {
        throw new Error("Unable to resolve Clerk domain for UI bundle load.");
      }

      const uiSrc = `https://${domain}/npm/@clerk/ui@1/dist/ui.browser.js`;
      await loadScript(uiSrc);
      clerkUiLoaded = true;
    }

    if (clerkLoaded && publishableKey === resolvedKey) {
      return window.Clerk;
    }

    publishableKey = resolvedKey;
    await window.Clerk.load({
      publishableKey: resolvedKey,
      frontendApi: resolvedFrontendApi || undefined,
      ui: { ClerkUI: window.__internal_ClerkUICtor }
    });
    clerkLoaded = true;
    return window.Clerk;
  }

  function decodePayload(token) {
    if (!token) return {};
    const payload = token.split(".")[1];
    if (!payload) return {};
    const json = atob(payload.replace(/-/g, "+").replace(/_/g, "/"));
    return JSON.parse(json);
  }

  return {
    async mountSignIn(elementId, key, frontendApi, returnUrl) {
      const clerk = await ensureLoaded(key, frontendApi);
      const el = document.getElementById(elementId);
      if (!el) throw new Error(`Element '${elementId}' not found`);

      const redirectUrl = returnUrl || "/tournaments";
      clerk.mountSignIn(el, {
        afterSignInUrl: redirectUrl,
        afterSignUpUrl: redirectUrl
      });
    },

    async unmountSignIn(elementId) {
      if (!window.Clerk) return;
      const el = document.getElementById(elementId);
      if (!el) return;
      window.Clerk.unmountSignIn(el);
    },

    async getSessionInfo(key, frontendApi) {
      const clerk = await ensureLoaded(key, frontendApi);
      const config = await getClientConfig();
      const session = clerk.session;
      if (!session) {
        return null;
      }

      const token = await session.getToken();
      const tokenWithTemplate = config.jwtTemplate
        ? await session.getToken({ template: config.jwtTemplate })
        : token;

      const resolvedToken = tokenWithTemplate || token;
      if (!resolvedToken) {
        return null;
      }

      const payload = decodePayload(resolvedToken);
      const user = clerk.user;
      return {
        token: resolvedToken,
        email: user?.primaryEmailAddress?.emailAddress ?? payload.email ?? payload.email_address ?? null,
        userId: user?.id ?? payload.sub ?? null,
        organizationId: payload.org_id ?? payload.orgId ?? null,
        role: payload.org_role ?? payload.orgRole ?? payload.role ?? null,
        expiresUtc: payload.exp ? new Date(payload.exp * 1000).toISOString() : null
      };
    },

    async signOut(key, frontendApi) {
      const clerk = await ensureLoaded(key, frontendApi);
      await clerk.signOut();
    },

    async redirectToSignIn(key, frontendApi, returnUrl) {
      const clerk = await ensureLoaded(key, frontendApi);
      const target = returnUrl || "/tournaments";
      await clerk.redirectToSignIn({
        afterSignInUrl: target,
        afterSignUpUrl: target
      });
    },

    async getConfig() {
      return await getClientConfig();
    }
  };
})();
