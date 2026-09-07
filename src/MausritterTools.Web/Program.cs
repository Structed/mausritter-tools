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
await host.Services.GetRequiredService<LocaleState>().InitialiseAsync();

await host.RunAsync();
