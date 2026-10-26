using Dbvprovas.Api.Infrastructure;
using Dbvprovas.Api.Modules.Identity;
using Dbvprovas.Api.Modules.Organization;
using Dbvprovas.Api.Modules.Tenancy;
using Dbvprovas.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Dbvprovas.Api.Modules.Notifications;

// Avisos do R1 (RN-NTF-001, RN-NTF-002, D-145): uma linha por destinatário na caixa pessoal, no mesmo SaveChanges da
// decisão. O e-mail só para a decisão de fundação, pela fila de saída enviada depois do commit (CA-NTF-001). O aviso
// não copia perfil, motivo nem credencial: só o evento e IDs. O ObjectId identifica a ocorrência (uma por evento).
public sealed class Notifier(AppDbContext db, ClubContext context, TimeProvider clock)
{
    public void Notify(NotificationEvent evt, Guid objectId, Guid? clubId, IEnumerable<Guid> recipientPersonIds)
    {
        var now = clock.GetUtcNow();
        foreach (var person in recipientPersonIds.Distinct())
            db.Notifications.Add(new Notification
            {
                Id = Guid.CreateVersion7(), RecipientPersonId = person, Event = evt, ClubId = clubId, ObjectId = objectId, CreatedAt = now,
            });
    }

    public void QueueEmail(EmailTemplate template, Guid objectId, Guid recipientAccountId) =>
        db.EmailOutbox.Add(new EmailOutboxMessage
        {
            Id = Guid.CreateVersion7(), Template = template, ObjectId = objectId, RecipientAccountId = recipientAccountId, CreatedAt = clock.GetUtcNow(),
        });

    // Diretoria vigente do clube da requisição (D-011): cargo de diretoria vigente, participação ativa e 18+.
    public Task<IReadOnlyList<Guid>> BoardAsync(CancellationToken ct) => HoldersAsync(RoleRules.IsBoard, ct);

    public Task<IReadOnlyList<Guid>> DirectorAndSecretaryAsync(CancellationToken ct) =>
        HoldersAsync(role => role is RoleCode.Director or RoleCode.Secretary, ct);

    // Admins com 18 anos ou mais (D-146), pela função do banco: o app não lê a conta nem o nascimento deles.
    public async Task<IReadOnlyList<Guid>> PlatformAdminsAsync(CancellationToken ct)
    {
        var bornOnOrBefore = BrasiliaCalendar.Today(clock).AddYears(-18);
        return await db.Database.SqlQuery<Guid>($"SELECT adult_platform_admin_ids({bornOnOrBefore}) AS \"Value\"").ToListAsync(ct);
    }

    private async Task<IReadOnlyList<Guid>> HoldersAsync(Func<RoleCode, bool> roles, CancellationToken ct)
    {
        var clubId = context.ClubId ?? throw new InvalidOperationException("Club context required (RN-NTF-002).");
        var rows = await (
            from r in db.RoleAssignments
            join m in db.Memberships on r.MembershipId equals m.Id
            join p in db.Persons on m.PersonId equals p.Id
            where r.ClubId == clubId && r.EndedAt == null && m.Status == MembershipStatus.Active
            select new { r.Role, m.PersonId, p.BirthDate }).ToListAsync(ct);
        var today = BrasiliaCalendar.Today(clock);
        return rows
            .Where(x => roles(x.Role) && x.BirthDate is { } birth && AgeCalculator.AgeOn(birth, today) >= 18)
            .Select(x => x.PersonId)
            .Distinct()
            .ToList();
    }
}
