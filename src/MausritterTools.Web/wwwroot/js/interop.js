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

// The language the visitor's browser asks for, used only to choose a default they can override.
export function browserLanguage() {
    return navigator.language || (navigator.languages && navigator.languages[0]) || '';
}

// Keeps the document in step with the chosen language: `lang` drives hyphenation, spell-checking
// and how a screen reader pronounces the page, none of which Blazor sets for us.
export function applyLanguage(code, description, errorMessage, reloadLabel) {
    document.documentElement.lang = code;

    const meta = document.querySelector('meta[name="description"]');
    if (meta && description) {
        meta.setAttribute('content', description);
    }

    // The error banner is static markup in index.html, so it is the one piece of UI Blazor never
    // re-renders and the only one that has to be translated by hand.
    const errorUi = document.getElementById('blazor-error-ui');
    if (errorUi && errorMessage) {
        const reload = errorUi.querySelector('.reload');
        const dismiss = errorUi.querySelector('.dismiss');

        errorUi.textContent = errorMessage + ' ';

        if (reload) {
            reload.textContent = reloadLabel;
            errorUi.appendChild(reload);
        }

        if (dismiss) {
            errorUi.appendChild(dismiss);
        }
    }
}
