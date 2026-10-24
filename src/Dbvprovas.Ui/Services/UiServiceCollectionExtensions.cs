using Dbvprovas.Ui.Services.Api;
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
        services.AddScoped<IdentityApi>();
        services.AddScoped<NotificationsApi>();
        services.AddScoped<FoundingApi>();
        services.AddScoped<OrganizationApi>();
        services.AddScoped<JoiningApi>();
        services.AddScoped<PrivacyApi>();
        services.AddScoped<AuditApi>();
        return services;
    }
}
