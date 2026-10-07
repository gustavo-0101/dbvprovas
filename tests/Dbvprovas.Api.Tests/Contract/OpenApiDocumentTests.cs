using System.Text.Json;
using System.Text.Json.Nodes;
using Dbvprovas.Api.Tests.Infrastructure;
using Dbvprovas.TestSupport;

namespace Dbvprovas.Api.Tests.Contract;

public sealed class OpenApiDocumentTests(PostgresFixture db)
{
    private static readonly string CommittedPath = Path.Combine(RepoPaths.Root, "src", "Dbvprovas.Api", "openapi", "v1.json");

    // D-100, RNF-AUD-002.
    // Para atualizar o arquivo: UPDATE_OPENAPI=1 dotnet test tests/Dbvprovas.Api.Tests --filter OpenApi
    [Fact]
    public async Task OpenApi_document_matches_committed_file()
    {
        await using var api = new ApiFactory(db, "Development");
        var json = await api.CreateClient().GetStringAsync("/openapi/v1.json");
        var normalized = JsonNode.Parse(json)!.ToJsonString(new JsonSerializerOptions { WriteIndented = true, NewLine = "\n" }) + "\n";

        if (Environment.GetEnvironmentVariable("UPDATE_OPENAPI") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CommittedPath)!);
            await File.WriteAllTextAsync(CommittedPath, normalized);
            return;
        }

        Assert.True(File.Exists(CommittedPath), "Documento OpenAPI ausente; gere com UPDATE_OPENAPI=1.");
        Assert.Equal((await File.ReadAllTextAsync(CommittedPath)).ReplaceLineEndings("\n"), normalized);
    }
}
