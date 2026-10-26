using System.Text.Encodings.Web;
using Dbvprovas.Api.Infrastructure;
using Dbvprovas.Api.Modules.Tenancy;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dbvprovas.Api.Modules.Identity;

// D-120
public static class DevSession
{
    public const string Scheme = "DevSession";
}

public static class Policies
{
    // Pessoa achada pela conta da sessão nesta requisição (PC-09).
    public const string Member = "member";

    // Admin da plataforma, lido nesta requisição e só com 18+ (RN-AUD-005, D-146).
    public const string PlatformAdmin = "platform-admin";
}

public sealed class PersonRequirement : IAuthorizationRequirement;

public sealed class PlatformAdminRequirement : IAuthorizationRequirement;

public sealed class PersonContextAuthorizationHandler(ClubContext context) : IAuthorizationHandler
{
    public Task HandleAsync(AuthorizationHandlerContext authorization)
    {
        foreach (var requirement in authorization.PendingRequirements.ToList())
        {
            var met = requirement switch
            {
                PersonRequirement => context.PersonId is not null,
                PlatformAdminRequirement => context.PersonId is not null && context.IsPlatformAdmin,
                _ => false,
            };
            if (met)
                authorization.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

public static class SessionSetup
{
    public static void AddSessions(this WebApplicationBuilder builder)
    {
        if (builder.Environment.IsDevelopment())
        {
            builder.Services.AddAuthentication(DevSession.Scheme)
                .AddBearerToken(DevSession.Scheme, o => o.BearerTokenExpiration = TimeSpan.FromHours(8));
        }
        else
        {
            // RF-ID-003
            builder.Services.AddAuthentication(NoSessionHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, NoSessionHandler>(NoSessionHandler.SchemeName, null);
        }

        // RNF-TEN-002, PC-09
        builder.Services.AddScoped<IAuthorizationHandler, PersonContextAuthorizationHandler>();
        builder.Services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(Policies.Member, p => p.RequireAuthenticatedUser().AddRequirements(new PersonRequirement()))
            .AddPolicy(Policies.PlatformAdmin, p => p.RequireAuthenticatedUser().AddRequirements(new PlatformAdminRequirement()));
    }

    // PC-09: a sessão traz só a conta; pessoa e papel de admin são lidos aqui, a cada requisição.
    public static IApplicationBuilder UsePersonContext(this IApplicationBuilder app) =>
        app.Use(async (http, next) =>
        {
            if (Guid.TryParse(http.User.FindFirst(DbvClaims.AccountId)?.Value, out var accountId))
            {
                var services = http.RequestServices;
                var context = services.GetRequiredService<ClubContext>();
                var db = services.GetRequiredService<AppDbContext>();
                var account = await db.Accounts.Where(a => a.Id == accountId)
                    .Select(a => new { a.PersonId, a.IsPlatformAdmin })
                    .SingleOrDefaultAsync(http.RequestAborted);
                if (account is not null)
                {
                    context.AccountId = accountId;
                    context.PersonId = account.PersonId;
                    if (account.IsPlatformAdmin)
                    {
                        // D-146: a marca de admin só conta com 18 anos ou mais.
                        var birth = await db.Persons.Where(p => p.Id == account.PersonId).Select(p => p.BirthDate).SingleOrDefaultAsync(http.RequestAborted);
                        var today = BrasiliaCalendar.Today(services.GetRequiredService<TimeProvider>());
                        context.IsPlatformAdmin = birth is { } date && AgeCalculator.AgeOn(date, today) >= 18;
                    }
                }
            }

            await next(http);
        });
}

public sealed class NoSessionHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "NoSession";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
        Task.FromResult(AuthenticateResult.NoResult());
}
