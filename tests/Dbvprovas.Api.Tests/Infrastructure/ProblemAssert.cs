using System.Net;
using System.Text.Json;
using Dbvprovas.Contracts;

namespace Dbvprovas.Api.Tests.Infrastructure;

// D-125: a resposta 400, 403 ou 409 traz o código estável na extensão "code" do ProblemDetails.
public static class ProblemAssert
{
    public static async Task HasCodeAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        using (response)
        {
            Assert.Equal(status, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(code, json.RootElement.GetProperty(ProblemCodes.Extension).GetString());
        }
    }
}
