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
