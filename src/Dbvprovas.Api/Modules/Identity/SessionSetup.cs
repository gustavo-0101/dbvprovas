using System.Text.Encodings.Web;
using Dbvprovas.Api.Modules.Tenancy;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Dbvprovas.Api.Modules.Identity;

// D-120
public static class DevSession
{
    public const string Scheme = "DevSession";
}

public static class Policies
{
    public const string Member = "member";
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

        // RNF-TEN-002
        builder.Services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(Policies.Member, p => p.RequireAuthenticatedUser().RequireClaim(DbvClaims.PersonId));
    }

    public static IApplicationBuilder UsePersonContext(this IApplicationBuilder app) =>
        app.Use(async (http, next) =>
        {
            if (Guid.TryParse(http.User.FindFirst(DbvClaims.PersonId)?.Value, out var personId))
                http.RequestServices.GetRequiredService<ClubContext>().PersonId = personId;
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
