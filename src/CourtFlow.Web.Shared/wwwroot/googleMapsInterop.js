window.courtflowGoogleMaps = (() => {
  let loadPromise = null;

  function getApiKey() {
    const injected = window.__courtflowGoogleMapsConfig;
    if (injected?.apiKey) return injected.apiKey;

    return "";
  }

  let configPromise = null;

  async function resolveApiKey() {
    const direct = getApiKey();
    if (direct) return direct;

    if (!configPromise) {
      configPromise = fetch("/appsettings.json", { cache: "no-store" })
        .then(async response => {
          if (!response.ok) return "";
          const json = await response.json();
          return json?.GoogleMaps?.ApiKey ?? "";
        })
        .catch(() => "");
    }

    return configPromise;
  }

  function loadMapsApi() {
    return resolveApiKey().then(key => {
      if (!key) return Promise.reject(new Error("Google Maps API key is not configured."));
      if (window.google?.maps?.places) return Promise.resolve();
      if (loadPromise) return loadPromise;

      loadPromise = new Promise((resolve, reject) => {
        const script = document.createElement("script");
        script.src = `https://maps.googleapis.com/maps/api/js?key=${encodeURIComponent(key)}&libraries=places&loading=async`;
        script.async = true;
        script.dataset.courtflowGoogleMaps = "true";
        script.onload = () => resolve();
        script.onerror = () => reject(new Error("Failed to load Google Maps JavaScript API."));
        document.head.appendChild(script);
      });

      return loadPromise;
    });
  }

  async function attachAutocomplete(container, dotnetRef) {
    const key = await resolveApiKey();
    if (!key) return;

    await loadMapsApi();
    const input = container?.querySelector?.("input");
    if (!input) return;

    const autocomplete = new google.maps.places.Autocomplete(input, {
      fields: ["formatted_address"],
      types: ["address"]
    });

    autocomplete.addListener("place_changed", () => {
      const place = autocomplete.getPlace();
      const address = place?.formatted_address;
      if (address) {
        dotnetRef.invokeMethodAsync("OnPlaceSelected", address);
      }
    });
  }

  return {
    getApiKey: resolveApiKey,
    attachAutocomplete
  };
})();
