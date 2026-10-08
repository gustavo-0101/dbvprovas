using Dbvprovas.Ui.Services;
using Microsoft.Extensions.Logging;

namespace Dbvprovas.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();
        builder.Services.AddMauiBlazorWebView();

        // Mesmas telas e serviços do site; só mudam o endereço da API e a guarda da sessão (RNF-TEN-004).
        builder.Services.AddDbvprovasUi(ApiAddress.Current);
        builder.Services.AddScoped<ISessionStore, SecureSessionStore>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif
        return builder.Build();
    }
}
