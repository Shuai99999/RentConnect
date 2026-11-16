export async function getVisitorLocation(url) {
    const response = await fetch(url, { cache: "no-store" });
    return response.status === 200 ? await response.json() : null;
}