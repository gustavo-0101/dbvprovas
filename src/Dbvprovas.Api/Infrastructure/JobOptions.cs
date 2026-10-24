namespace Dbvprovas.Api.Infrastructure;

// Tarefas agendadas mínimas do R1 (D-172): ligadas em dev e produção; desligadas nos testes, que chamam RunOnceAsync.
public sealed class JobOptions
{
    public const string Section = "Jobs";

    public bool Enabled { get; set; } = true;

    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(5);
}
