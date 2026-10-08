using Dbvprovas.Ui.Services;
using Dbvprovas.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// A API e o site estão na mesma origem (D-112).
builder.Services.AddDbvprovasUi(new Uri(builder.HostEnvironment.BaseAddress));
builder.Services.AddScoped<ISessionStore, BrowserSessionStore>();

await builder.Build().RunAsync();
