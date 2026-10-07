using Dbvprovas.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Dbvprovas.Api.Modules.Tenancy;

// RNF-TEN-003: consulta a participação em cada pedido antes de fixar o clube (D-121).
public sealed class ClubMembershipFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext invocation, EndpointFilterDelegate next)
    {
        var http = invocation.HttpContext;
        var context = http.RequestServices.GetRequiredService<ClubContext>();
        var db = http.RequestServices.GetRequiredService<AppDbContext>();

        // D-117: clube alheio e clube inexistente recebem o mesmo 404.
        if (context.PersonId is not { } personId || !Guid.TryParse(http.GetRouteValue("clubId")?.ToString(), out var clubId))
            return TypedResults.NotFound();

        var isMember = await db.Memberships.AnyAsync(
            m => m.ClubId == clubId && m.PersonId == personId && m.EndedAt == null,
            http.RequestAborted);
        if (!isMember)
            return TypedResults.NotFound();

        context.ClubId = clubId;
        return await next(invocation);
    }
}
