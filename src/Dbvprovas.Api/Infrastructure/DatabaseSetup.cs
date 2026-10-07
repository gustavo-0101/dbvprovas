using Dbvprovas.Api.Modules.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace Dbvprovas.Api.Infrastructure;

public static class DatabaseSetup
{
    public static IServiceCollection AddDatabase(this IServiceCollection services)
    {
        services.AddScoped<ClubContext>();
        services.AddScoped<IInterceptor, ClubSessionInterceptor>();
        services.AddScoped<IInterceptor, ClubWriteGuardInterceptor>();

        services.AddSingleton(sp =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("App")
                ?? throw new InvalidOperationException("Connection string 'App' is missing.");
            var builder = new NpgsqlDataSourceBuilder(connectionString);
            builder.UseLoggerFactory(sp.GetRequiredService<ILoggerFactory>());
            return builder.Build();
        });

        services.AddDbContext<AppDbContext>((sp, options) => options
            .UseNpgsql(sp.GetRequiredService<NpgsqlDataSource>())
            .AddInterceptors(sp.GetServices<IInterceptor>()));
        return services;
    }
}
