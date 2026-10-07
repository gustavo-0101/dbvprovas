namespace Dbvprovas.Api.Infrastructure;

public static class RoutingSetup
{
    // Executado entre UseRouting e UseAuthorization (CA-ID-001, D-117).
    public static IApplicationBuilder UseUnmatchedRouteNotFound(this IApplicationBuilder app) =>
        app.Use(async (http, next) =>
        {
            if (http.GetEndpoint() is null)
            {
                http.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            await next(http);
        });
}
