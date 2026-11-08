using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using PokeArena.Web;
using PokeArena.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// The data host (R2 behind Cloudflare, ADR-0003 addendum); relative URLs resolve against the site itself.
var dataUrl = builder.Configuration["DataUrl"] is { Length: > 0 } configured
    ? configured
    : throw new InvalidOperationException("DataUrl is missing in wwwroot/appsettings.json (set by the Pages workflow).");
var dataBase = new Uri(new Uri(builder.HostEnvironment.BaseAddress), dataUrl.EndsWith('/') ? dataUrl : dataUrl + "/");
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped(_ => new SiteData(new HttpClient { BaseAddress = dataBase }));

await builder.Build().RunAsync();
