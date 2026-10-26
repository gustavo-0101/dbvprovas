using Dbvprovas.Api.Modules.Identity;
using Dbvprovas.Contracts;

namespace Dbvprovas.Api.Modules.Tenancy.Founding;

public static class FoundingEndpoints
{
    public static void MapFoundingRequests(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/founding-requests", async (CreateFoundingRequest body, FoundingRequestService service, CancellationToken ct) =>
                (await service.CreateAsync(body, ct)).ToHttp())
            .RequireAuthorization(Policies.Member).WithTags("Founding");

        var mine = app.MapGroup("/api/me/founding-requests").RequireAuthorization(Policies.Member).WithTags("Founding");
        mine.MapGet("", async (FoundingRequestService service, CancellationToken ct) => TypedResults.Ok(await service.ListMineAsync(ct)));
        mine.MapPost("/{requestId:guid}/cancel", async (Guid requestId, FoundingRequestService service, CancellationToken ct) =>
            (await service.CancelAsync(requestId, ct)).ToHttp());
    }
}
