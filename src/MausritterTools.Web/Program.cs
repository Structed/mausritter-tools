using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using MausritterTools.Web;
using MausritterTools.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<GameDataProvider>();
builder.Services.AddScoped<BrowserInterop>();
builder.Services.AddScoped<LocaleState>();

WebAssemblyHost host = builder.Build();

// Settled before the first render, so no component has to cope with the language arriving late and
// nothing is drawn in English only to be swapped out a frame later.
LocaleState locale = host.Services.GetRequiredService<LocaleState>();
NavigationManager navigation = host.Services.GetRequiredService<NavigationManager>();

await locale.InitialiseAsync(ReadLanguageFromUrl(navigation));

await host.RunAsync();

static string? ReadLanguageFromUrl(NavigationManager navigation)
{
    string query = navigation.ToAbsoluteUri(navigation.Uri).Query;

    foreach (string pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
    {
        string[] parts = pair.Split('=', 2);

        if (parts.Length == 2 &&
            Uri.UnescapeDataString(parts[0]).Equals(LocaleState.QueryParameter, StringComparison.OrdinalIgnoreCase))
        {
            return Uri.UnescapeDataString(parts[1]);
        }
    }

    return null;
}
