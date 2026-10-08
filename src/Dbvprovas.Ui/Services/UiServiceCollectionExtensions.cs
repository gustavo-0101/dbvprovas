using Microsoft.Extensions.DependencyInjection;

namespace Dbvprovas.Ui.Services;

public static class UiServiceCollectionExtensions
{
    // O endereço da API vem de cada alvo (RNF-TEN-004): a própria origem no site e a máquina
    // de dev no Android. O handler só é trocado nos testes.
    public static IServiceCollection AddDbvprovasUi(this IServiceCollection services, Uri apiBaseAddress, Func<HttpMessageHandler>? handler = null)
    {
        services.AddScoped(_ => new HttpClient(handler?.Invoke() ?? new HttpClientHandler())
        {
            BaseAddress = apiBaseAddress,
            Timeout = TimeSpan.FromSeconds(15),
        });
        services.AddScoped<SessionState>();
        services.AddScoped<ApiClient>();
        return services;
    }
}
