const sdkScriptUrl = "https://atlas.microsoft.com/sdk/javascript/mapcontrol/3/atlas.min.js";
const sdkStyleUrl = "https://atlas.microsoft.com/sdk/javascript/mapcontrol/3/atlas.min.css";

let map;
let popup;
let mapReady;
let rentalMarkers = [];
let userMarker;

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

export async function initializeMap(containerId, subscriptionKey, listings) {
    await loadAzureMaps();

    map = new atlas.Map(containerId, {
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

    mapReady = new Promise(resolve => {
        map.events.add("ready", () => {
            map.controls.add([
                new atlas.control.ZoomControl(),
                new atlas.control.CompassControl()
            ], { position: "top-right" });
            popup = new atlas.Popup({ closeButton: true, pixelOffset: [0, -18] });
            resolve();
        });
    });

    await mapReady;
    await updateListings(listings);
}

export async function updateListings(listings) {
    if (!map || !mapReady) {
        return;
    }

    await mapReady;
    popup?.close();
    for (const marker of rentalMarkers) {
        map.markers.remove(marker);
    }
    rentalMarkers = [];

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
        map.markers.add(marker);
        map.events.add("click", marker, () => openPropertyPopup(property, position));
        rentalMarkers.push(marker);
    }

    if (positions.length === 1) {
        map.setCamera({ center: positions[0], zoom: 13, type: "ease", duration: 500 });
    } else if (positions.length > 1) {
        map.setCamera({
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

function openPropertyPopup(property, position) {
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

    popup.setOptions({ position, content });
    popup.open(map);
}

export function locateUser() {
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
                showUserLocation(result);
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

async function showUserLocation(location) {
    if (!map || !mapReady) {
        return;
    }

    await mapReady;
    if (userMarker) {
        map.markers.remove(userMarker);
    }

    const dot = document.createElement("div");
    dot.className = "rent-user-location";
    dot.setAttribute("aria-label", "Your approximate location");
    userMarker = new atlas.HtmlMarker({
        position: [location.longitude, location.latitude],
        htmlContent: dot,
        anchor: "center"
    });
    map.markers.add(userMarker);
    map.setCamera({ center: [location.longitude, location.latitude], zoom: 11, type: "ease", duration: 600 });
}

export function disposeMap() {
    popup?.close();
    map?.dispose();
    map = undefined;
    popup = undefined;
    mapReady = undefined;
    rentalMarkers = [];
    userMarker = undefined;
}
