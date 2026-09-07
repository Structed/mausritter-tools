// Browser capabilities the settlement generator needs that Blazor does not expose directly.
// Loaded as a module by BrowserInterop.

export function downloadText(fileName, contentType, text) {
    const blob = new Blob([text], { type: contentType });
    const url = URL.createObjectURL(blob);

    const link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);

    // Revoking immediately can cancel the download in some browsers, so give it a moment.
    setTimeout(() => URL.revokeObjectURL(url), 10000);
}

export async function copyText(text) {
    // navigator.clipboard is unavailable outside secure contexts, so fall back to a hidden
    // textarea and the legacy command rather than silently doing nothing.
    if (navigator.clipboard && window.isSecureContext) {
        try {
            await navigator.clipboard.writeText(text);
            return true;
        } catch {
            // Fall through to the legacy path below.
        }
    }

    const area = document.createElement('textarea');
    area.value = text;
    area.setAttribute('readonly', '');
    area.style.position = 'fixed';
    area.style.opacity = '0';
    document.body.appendChild(area);
    area.select();

    let copied = false;
    try {
        copied = document.execCommand('copy');
    } catch {
        copied = false;
    } finally {
        document.body.removeChild(area);
    }

    return copied;
}

export function readStorage(key) {
    try {
        return window.localStorage.getItem(key);
    } catch {
        // Storage throws when disabled, for example in private browsing modes.
        return null;
    }
}

export function writeStorage(key, value) {
    try {
        window.localStorage.setItem(key, value);
    } catch {
        // Losing autosave is not worth breaking the page over.
    }
}

export function clearStorage(key) {
    try {
        window.localStorage.removeItem(key);
    } catch {
        // Ignore.
    }
}

export function printPage() {
    window.print();
}
