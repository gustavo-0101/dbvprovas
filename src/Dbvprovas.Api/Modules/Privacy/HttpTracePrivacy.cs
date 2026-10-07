using System.Diagnostics;
using Microsoft.AspNetCore.Routing;

namespace Dbvprovas.Api.Modules.Privacy;

// RNF-PRV-001, RNF-PRV-002
internal static class HttpTracePrivacy
{
    public static void Protect(Activity activity, HttpContext context, bool response)
    {
        foreach (var key in new[]
        {
            "url.query", "url.full", "http.url", "http.target", "http.host", "server.address",
            "net.host.name", "user_agent.original", "http.user_agent", "http.request.method_original",
        })
            activity.SetTag(key, null);
        foreach (var (key, _) in activity.Baggage.ToArray())
            activity.SetBaggage(key, null);
        activity.TraceStateString = null;

        var route = response ? (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText : null;
        var method = context.Request.Method is "GET" or "POST" or "PUT" or "PATCH" or "DELETE"
            or "HEAD" or "OPTIONS" or "CONNECT" or "TRACE" ? context.Request.Method : "_OTHER";
        activity.SetTag("http.request.method", method);
        activity.SetTag("http.route", route);
        activity.SetTag("url.path", route ?? "[unmatched]");
        activity.DisplayName = route is null ? method : $"{method} {route}";
    }
}
