using Microsoft.JSInterop;

namespace MausritterTools.Web.Services;

/// <summary>
/// Wraps the browser features the generator needs: downloads, the clipboard, local storage and
/// printing.
/// </summary>
/// <remarks>
/// Every call is defensive. A blocked clipboard or disabled local storage should degrade quietly
/// rather than throw an unhandled exception into a Blazor render cycle.
/// </remarks>
public sealed class BrowserInterop(IJSRuntime jsRuntime) : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime =
        jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));

    private IJSObjectReference? _module;

    private async Task<IJSObjectReference> ModuleAsync() =>
        _module ??= await _jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/interop.js");

    /// <summary>Offers a text file to the user as a download.</summary>
    public async Task DownloadTextAsync(string fileName, string contentType, string text)
    {
        IJSObjectReference module = await ModuleAsync();
        await module.InvokeVoidAsync("downloadText", fileName, contentType, text);
    }

    /// <summary>Copies text to the clipboard, reporting whether it worked.</summary>
    public async Task<bool> CopyTextAsync(string text)
    {
        try
        {
            IJSObjectReference module = await ModuleAsync();
            return await module.InvokeAsync<bool>("copyText", text);
        }
        catch (JSException)
        {
            return false;
        }
    }

    public async Task<string?> ReadStorageAsync(string key)
    {
        try
        {
            IJSObjectReference module = await ModuleAsync();
            return await module.InvokeAsync<string?>("readStorage", key);
        }
        catch (JSException)
        {
            return null;
        }
    }

    public async Task WriteStorageAsync(string key, string value)
    {
        try
        {
            IJSObjectReference module = await ModuleAsync();
            await module.InvokeVoidAsync("writeStorage", key, value);
        }
        catch (JSException)
        {
            // Autosave is a convenience; failing to store must not break the page.
        }
    }

    public async Task ClearStorageAsync(string key)
    {
        try
        {
            IJSObjectReference module = await ModuleAsync();
            await module.InvokeVoidAsync("clearStorage", key);
        }
        catch (JSException)
        {
            // Ignore.
        }
    }

    public async Task PrintAsync()
    {
        IJSObjectReference module = await ModuleAsync();
        await module.InvokeVoidAsync("printPage");
    }

    /// <summary>The language the browser asks for, used only as a first guess.</summary>
    public async Task<string?> GetBrowserLanguageAsync()
    {
        try
        {
            IJSObjectReference module = await ModuleAsync();
            return await module.InvokeAsync<string?>("browserLanguage");
        }
        catch (JSException)
        {
            return null;
        }
    }

    /// <summary>
    /// Reads a query parameter from the address bar.
    /// </summary>
    /// <remarks>
    /// Read through JavaScript rather than Blazor's <c>NavigationManager</c>, which is not
    /// initialised until the app is running. The language has to be settled before the first render,
    /// which is earlier than that.
    /// </remarks>
    public async Task<string?> GetQueryParameterAsync(string name)
    {
        try
        {
            IJSObjectReference module = await ModuleAsync();
            return await module.InvokeAsync<string?>("queryParameter", name);
        }
        catch (JSException)
        {
            return null;
        }
    }

    /// <summary>
    /// Tells the document which language it is in, and translates the one banner Blazor never
    /// renders itself.
    /// </summary>
    public async Task ApplyLanguageAsync(
        string code, string description, string errorMessage, string reloadLabel)
    {
        try
        {
            IJSObjectReference module = await ModuleAsync();
            await module.InvokeVoidAsync("applyLanguage", code, description, errorMessage, reloadLabel);
        }
        catch (JSException)
        {
            // Cosmetic; a page that renders in the right language with the wrong `lang` attribute
            // is far better than one that fails to start.
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is null)
        {
            return;
        }

        try
        {
            await _module.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // The circuit is already gone; nothing to release.
        }
    }
}
