namespace Dbvprovas.Api.Tests.Infrastructure;

// CA-AUD-004 (extensão R1): valores fictícios únicos para os fluxos de cada módulo e a conferência de que nenhum
// deles aparece em logs, traces nem corpos de erro. A resposta de sucesso autorizada pode conter o próprio dado.
public sealed class LeakProbe : IDisposable
{
    private readonly List<string> _tracked = [];

    public string Name { get; } = $"Sentinela {Guid.NewGuid():N}";
    public string ClubName { get; } = $"Clube Sentinela {Guid.NewGuid():N}";
    public string City { get; } = $"Cidade Sentinela {Guid.NewGuid():N}";
    public string Email { get; } = $"sentinela{Guid.NewGuid():N}@example.com";
    public string Token { get; } = $"token-sentinela-{Guid.NewGuid():N}";
    public string GoogleSubject { get; } = $"sub-sentinela-{Guid.NewGuid():N}";

    // Data que não aparece em nenhum outro dado fictício; procurada nos dois formatos que um log usaria.
    public DateOnly BirthDate { get; } = new(1971, 4, 3);

    public ActivityCapture Traces { get; } = new();
    public List<string> ErrorBodies { get; } = [];

    public IEnumerable<string> Values =>
        [Name, ClubName, City, Email, Token, GoogleSubject, BirthDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            BirthDate.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture), .. _tracked];

    // Valor que o fluxo gerou e também não pode vazar (o código de convite do clube criado, por exemplo).
    public void Track(string value) => _tracked.Add(value);

    // Cada canal precisa ter sido capturado (logs e traces, separados) e nenhum pode conter um valor sentinela.
    public void AssertAbsent(params ApiFactory[] factories)
    {
        var logs = factories.SelectMany(f => f.Logs.Logs.Select(log => log.Text)).ToList();
        var traces = Traces.Items.ToList();
        Assert.NotEmpty(logs);
        Assert.NotEmpty(traces);
        foreach (var value in Values)
        {
            Assert.DoesNotContain(logs, text => text.Contains(value, StringComparison.Ordinal));
            Assert.DoesNotContain(traces, text => text.Contains(value, StringComparison.Ordinal));
            Assert.DoesNotContain(ErrorBodies, text => text.Contains(value, StringComparison.Ordinal));
        }
    }

    public void Dispose() => Traces.Dispose();
}
