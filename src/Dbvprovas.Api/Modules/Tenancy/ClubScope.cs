using System.Data;
using Dbvprovas.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Dbvprovas.Api.Modules.Tenancy;

// Troca explícita do clube da requisição, para os caminhos específicos da RNF-TEN-006: fundação, tarefas do admin,
// pedido próprio de entrada, controles próprios do titular e prazos agendados. Não é bypass: o filtro do EF e o RLS
// continuam valendo, agora para o clube escolhido. Só os arquivos de ClubScopeUsageTests a usam (CA-TEN-001).
// Antes de trocar, grave (SaveChangesAsync) o que pertence ao clube atual: o guarda de escrita compara cada
// entidade com o clube vigente na hora do SaveChanges.
public sealed class ClubScope(ClubContext context, AppDbContext db)
{
    public async Task EnterAsync(Guid clubId, CancellationToken ct)
    {
        if (db.ChangeTracker.Entries().Any(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Save pending changes before switching clubs (RN-TEN-001).");

        context.ClubId = clubId;
        // Conexão já aberta (transação em curso): o interceptor só roda na abertura, então grava aqui.
        if (db.Database.GetDbConnection().State == ConnectionState.Open)
            await db.Database.ExecuteSqlAsync($"SELECT set_config('app.club_id', {clubId.ToString()}, false)", ct);
    }
}
