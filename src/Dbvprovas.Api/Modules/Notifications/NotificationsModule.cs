namespace Dbvprovas.Api.Modules.Notifications;

// Registro do módulo NTF: caixa de avisos e envio do e-mail.
public static class NotificationsModule
{
    public static IServiceCollection AddNotificationsModule(this IServiceCollection services) => services;

    public static WebApplication MapNotificationsModule(this WebApplication app) => app;
}
