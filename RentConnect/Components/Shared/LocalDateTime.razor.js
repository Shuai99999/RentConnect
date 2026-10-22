const absoluteDateTimeOptions = {
    year: "numeric",
    month: "short",
    day: "numeric",
    hour: "numeric",
    minute: "2-digit"
};

export function formatLocalDateTime(element, utcValue, display) {
    const date = new Date(utcValue);

    if (Number.isNaN(date.getTime())) {
        return;
    }

    const localDateTime = new Intl.DateTimeFormat(
        undefined,
        absoluteDateTimeOptions).format(date);

    element.title = `${localDateTime} (${Intl.DateTimeFormat().resolvedOptions().timeZone})`;

    if (display === "Date") {
        element.textContent = new Intl.DateTimeFormat(undefined, {
            year: "numeric",
            month: "long",
            day: "numeric"
        }).format(date);
        return;
    }

    if (display === "Relative") {
        element.textContent = formatRelative(date) ?? new Intl.DateTimeFormat(undefined, {
            year: "numeric",
            month: "short",
            day: "numeric"
        }).format(date);
        return;
    }

    element.textContent = localDateTime;
}

export function localDateTimeToUtc(localValue) {
    const date = new Date(localValue);

    if (Number.isNaN(date.getTime())) {
        throw new Error("The local date and time is invalid.");
    }

    return date.toISOString();
}

function formatRelative(date) {
    const elapsedSeconds = Math.max(0, (Date.now() - date.getTime()) / 1000);

    if (elapsedSeconds < 60) return "Just now";
    if (elapsedSeconds < 3600) return `${Math.max(1, Math.floor(elapsedSeconds / 60))}m ago`;
    if (elapsedSeconds < 86400) return `${Math.max(1, Math.floor(elapsedSeconds / 3600))}h ago`;
    if (elapsedSeconds < 604800) return `${Math.max(1, Math.floor(elapsedSeconds / 86400))}d ago`;
    return null;
}
