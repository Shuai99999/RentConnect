const sdkScriptUrl = "https://atlas.microsoft.com/sdk/javascript/mapcontrol/3/atlas.min.js";
const sdkStyleUrl = "https://atlas.microsoft.com/sdk/javascript/mapcontrol/3/atlas.min.css";

let activeMap;
let lastMapConfiguration;
let recoveryTimer;
let recoveryInProgress = false;
let recoveryListenersRegistered = false;

function loadAzureMaps() {
    if (window.atlas) {
        return Promise.resolve();
    }

    const existing = document.querySelector(`script[src="${sdkScriptUrl}"]`);
    if (existing) {
        return new Promise((resolve, reject) => {
            existing.addEventListener("load", resolve, { once: true });
            existing.addEventListener("error", () => reject(new Error("Azure Maps failed to load.")), { once: true });
        });
    }

    if (!document.querySelector(`link[href="${sdkStyleUrl}"]`)) {
        const stylesheet = document.createElement("link");
        stylesheet.rel = "stylesheet";
        stylesheet.href = sdkStyleUrl;
        document.head.appendChild(stylesheet);
    }

    return new Promise((resolve, reject) => {
        const script = document.createElement("script");
        script.src = sdkScriptUrl;
        script.async = true;
        script.addEventListener("load", resolve, { once: true });
        script.addEventListener("error", () => reject(new Error("Azure Maps failed to load.")), { once: true });
        document.head.appendChild(script);
    });
}

export async function initializeMap(instanceId, containerId, subscriptionKey, listings) {
    window.clearTimeout(recoveryTimer);
    await loadAzureMaps();

    const container = document.getElementById(containerId);
    if (!container) {
        throw new Error(`Map container '${containerId}' was not found.`);
    }

    registerMapRecoveryListeners();
    lastMapConfiguration = { instanceId, containerId, subscriptionKey, listings: listings ?? [] };

    activeMap?.resolveReady?.();
    activeMap?.map.dispose();
    container.replaceChildren();

    const map = new atlas.Map(container, {
        authOptions: {
            authType: atlas.AuthenticationType.subscriptionKey,
            subscriptionKey
        },
        center: [-114.35, 54.1],
        zoom: 5,
        language: "en-CA",
        view: "Auto",
        style: "road_shaded_relief"
    });

    const state = {
        instanceId,
        container,
        map,
        popup: undefined,
        mapReady: undefined,
        resolveReady: undefined,
        rentalMarkers: [],
        userMarker: undefined
    };
    activeMap = state;

    state.mapReady = new Promise(resolve => {
        state.resolveReady = resolve;
        map.events.add("ready", () => {
            if (activeMap !== state) {
                resolve();
                return;
            }

            map.controls.add([
                new atlas.control.ZoomControl(),
                new atlas.control.CompassControl()
            ], { position: "top-right" });
            state.popup = new atlas.Popup({ closeButton: true, pixelOffset: [0, -18] });
            resolve();
        });
    });

    await state.mapReady;
    await updateListings(instanceId, listings);
}

export async function updateListings(instanceId, listings) {
    const state = getActiveMap(instanceId);
    if (!state) {
        return;
    }

    await state.mapReady;
    if (activeMap !== state) {
        return;
    }

    if (lastMapConfiguration?.instanceId === instanceId) {
        lastMapConfiguration.listings = listings ?? [];
    }

    state.popup?.close();
    for (const marker of state.rentalMarkers) {
        state.map.markers.remove(marker);
    }
    state.rentalMarkers = [];

    const properties = groupByProperty(listings ?? []);
    const positions = [];

    for (const property of properties) {
        const position = [property.longitude, property.latitude];
        positions.push(position);

        const button = document.createElement("button");
        button.type = "button";
        button.className = "rent-map-marker";
        button.setAttribute("aria-label", `${property.propertyName}, ${property.listings.length} available rental spaces`);
        const price = document.createElement("span");
        price.textContent = `$${Math.min(...property.listings.map(item => Number(item.monthlyRent))).toLocaleString("en-CA")}`;
        button.appendChild(price);

        const marker = new atlas.HtmlMarker({
            position,
            htmlContent: button,
            anchor: "bottom"
        });
        state.map.markers.add(marker);
        state.map.events.add("click", marker, () => openPropertyPopup(state, property, position));
        state.rentalMarkers.push(marker);
    }

    if (positions.length === 1) {
        state.map.setCamera({ center: positions[0], zoom: 13, type: "ease", duration: 500 });
    } else if (positions.length > 1) {
        state.map.setCamera({
            bounds: atlas.data.BoundingBox.fromPositions(positions),
            padding: 70,
            maxZoom: 14,
            type: "ease",
            duration: 500
        });
    }
}

function groupByProperty(listings) {
    const groups = new Map();
    for (const listing of listings) {
        if (!Number.isFinite(listing.latitude) || !Number.isFinite(listing.longitude)) {
            continue;
        }

        let property = groups.get(listing.propertyId);
        if (!property) {
            property = {
                propertyId: listing.propertyId,
                propertyName: listing.propertyName,
                city: listing.city,
                region: listing.region,
                latitude: listing.latitude,
                longitude: listing.longitude,
                listings: []
            };
            groups.set(listing.propertyId, property);
        }
        property.listings.push(listing);
    }
    return [...groups.values()];
}

function openPropertyPopup(state, property, position) {
    if (activeMap !== state || !state.popup) {
        return;
    }

    const content = document.createElement("div");
    content.className = "rent-map-popup";

    const heading = document.createElement("h3");
    heading.textContent = property.propertyName;
    content.appendChild(heading);

    const location = document.createElement("p");
    location.textContent = [property.city, property.region].filter(Boolean).join(", ");
    content.appendChild(location);

    for (const listing of property.listings) {
        const link = document.createElement("a");
        link.href = `/listings/${listing.id}`;
        const title = document.createElement("span");
        title.textContent = listing.title;
        const price = document.createElement("span");
        price.className = "rent-map-popup-price";
        price.textContent = `$${Number(listing.monthlyRent).toLocaleString("en-CA")}/mo`;
        link.append(title, price);
        content.appendChild(link);
    }

    state.popup.setOptions({ position, content });
    state.popup.open(state.map);
}

export function locateUser(instanceId) {
    if (!navigator.geolocation) {
        return Promise.reject(new Error("Location is not supported by this browser."));
    }

    return new Promise((resolve, reject) => {
        navigator.geolocation.getCurrentPosition(
            position => {
                const result = {
                    latitude: position.coords.latitude,
                    longitude: position.coords.longitude
                };
                showUserLocation(instanceId, result);
                resolve(result);
            },
            error => {
                const message = error.code === error.PERMISSION_DENIED
                    ? "Location permission was denied."
                    : "The current location is unavailable.";
                reject(new Error(message));
            },
            { enableHighAccuracy: false, timeout: 10000, maximumAge: 300000 }
        );
    });
}

async function showUserLocation(instanceId, location) {
    const state = getActiveMap(instanceId);
    if (!state) {
        return;
    }

    await state.mapReady;
    if (activeMap !== state) {
        return;
    }

    if (state.userMarker) {
        state.map.markers.remove(state.userMarker);
    }

    const dot = document.createElement("div");
    dot.className = "rent-user-location";
    dot.setAttribute("aria-label", "Your approximate location");
    state.userMarker = new atlas.HtmlMarker({
        position: [location.longitude, location.latitude],
        htmlContent: dot,
        anchor: "center"
    });
    state.map.markers.add(state.userMarker);
    state.map.setCamera({ center: [location.longitude, location.latitude], zoom: 11, type: "ease", duration: 600 });
}

export function disposeMap(instanceId) {
    const state = getActiveMap(instanceId);
    if (!state) {
        return;
    }

    state.popup?.close();
    state.resolveReady?.();
    state.map.dispose();
    activeMap = undefined;
}

function getActiveMap(instanceId) {
    return activeMap?.instanceId === instanceId ? activeMap : undefined;
}

function registerMapRecoveryListeners() {
    if (recoveryListenersRegistered) {
        return;
    }

    recoveryListenersRegistered = true;
    window.addEventListener("pageshow", queueMapRecovery);
    window.addEventListener("popstate", queueMapRecovery);
    window.Blazor?.addEventListener("enhancedload", queueMapRecovery);
}

function queueMapRecovery() {
    window.clearTimeout(recoveryTimer);
    recoveryTimer = window.setTimeout(restoreMapIfNeeded, 500);
}

async function restoreMapIfNeeded() {
    if (recoveryInProgress) {
        return;
    }

    recoveryInProgress = true;
    try {
        const configuration = lastMapConfiguration;
        if (!configuration) {
            return;
        }

        const container = document.getElementById(configuration.containerId);
        if (!container) {
            return;
        }

        if (activeMap?.container === container) {
            const state = activeMap;
            await state.mapReady;
            if (activeMap !== state) {
                return;
            }

            const latestListings = lastMapConfiguration?.listings ?? [];
            const expectedMarkers = groupByProperty(latestListings).length;
            const renderedMarkers = container.querySelectorAll(".rent-map-marker").length;
            if (renderedMarkers !== expectedMarkers) {
                await updateListings(state.instanceId, latestListings);
            }
            return;
        }

        await initializeMap(
            configuration.instanceId,
            configuration.containerId,
            configuration.subscriptionKey,
            configuration.listings);
    } catch (error) {
        console.error("The rental map could not be restored after navigation.", error);
    } finally {
        recoveryInProgress = false;
    }
}
