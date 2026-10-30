using Dbvprovas.Api.Infrastructure;
using Dbvprovas.Api.Modules.Audit;
using Dbvprovas.Api.Modules.Identity;
using Dbvprovas.Api.Modules.Joining;
using Dbvprovas.Api.Modules.Privacy;
using Dbvprovas.Api.Modules.Tenancy;
using Dbvprovas.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Dbvprovas.Api.Modules.Tenancy.Founding;

// Pedido próprio de fundação (RF-TEN-005, PERM-TEN-001, RN-TEN-003, D-157). Plataforma e próprio: só o titular.
public sealed class FoundingRequestService(AppDbContext db, ClubContext context, AuditLog audit, TimeProvider clock)
{
    public async Task<Outcome<FoundingRequestResponse>> CreateAsync(CreateFoundingRequest body, CancellationToken ct)
    {
        var personId = context.PersonId!.Value;
        var accountId = context.AccountId!.Value;
        var name = TextInput.Clean(body.ClubName, 120);
        var city = TextInput.Clean(body.City, 120);
        var church = TextInput.Clean(body.Church, 120, required: false);
        if (name is null || city is null || church is null)
            return Outcome.Invalid(ProblemCodes.ValidationText);
        if (!body.ConsentAccepted)
            return Outcome.Invalid(ProblemCodes.ConsentRequired);
        if (body.ConsentTermVersion != ConsentTerms.AdultOwnParticipationVersion)
            return Outcome.Invalid(ProblemCodes.ConsentOutdated);

        var person = await db.Persons.Where(p => p.Id == personId).Select(p => new { p.Name, p.BirthDate, p.Sex }).SingleAsync(ct);
        if (person.Name is null || person.BirthDate is not { } birth || person.Sex is null)
            return Outcome.Forbidden(ProblemCodes.ProfileIncomplete);
        if (AgeCalculator.AgeOn(birth, BrasiliaCalendar.Today(clock)) < 18)
            return Outcome.Forbidden(ProblemCodes.AgeBelowMinimum);
        // Os próprios cargos são visíveis sem clube (filtro do EF e RLS).
        if (await db.RoleAssignments.AnyAsync(r => r.Role == RoleCode.Director && r.EndedAt == null, ct))
            return Outcome.Forbidden(ProblemCodes.FoundingIsDirector);
        var foundedBefore = await db.FoundingRecords.AnyAsync(r => r.PersonId == personId, ct);
        if (foundedBefore && !body.RequestException)
            return Outcome.Forbidden(ProblemCodes.FoundingAlreadyFounder);
        if (!foundedBefore && body.RequestException)
            return Outcome.Invalid(ProblemCodes.FoundingExceptionNotApplicable);

        var now = clock.GetUtcNow();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (!await PendingRequestGuard.TryAcquireAsync(db, personId, now, ct))
            return Outcome.Conflict(ProblemCodes.PendingRequestExists);

        var request = new FoundingRequest
        {
            Id = Guid.CreateVersion7(), RequesterPersonId = personId, RequesterAccountId = accountId, ClubName = name, ClubCity = city,
            ClubChurch = church.Length == 0 ? null : church, ProposedClubId = Guid.CreateVersion7(), ExceptionRequested = body.RequestException,
            CreatedAt = now,
        };
        // Consentimento pré-fundação: referencia o pedido enquanto não há clube (S-073, S-086).
        var consent = new Consent
        {
            Id = Guid.CreateVersion7(), PersonId = personId, FoundingRequestId = request.Id, TermVersion = body.ConsentTermVersion,
            ConsentedByAccountId = accountId, GrantedAt = now,
        };
        db.FoundingRequests.Add(request);
        db.Consents.Add(consent);
        audit.Record(AuditAction.FoundingRequested, AuditTarget.FoundingRequest, request.Id, null, AuditScope.Platform, personId);
        audit.Record(AuditAction.ConsentGranted, AuditTarget.Consent, consent.Id, null, AuditScope.Platform, personId);
        try
        {
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException e) when (DbErrors.IsUniqueViolation(e, DbConstraints.OnePendingFounding))
        {
            return Outcome.Conflict(ProblemCodes.PendingRequestExists);
        }

        return Outcome<FoundingRequestResponse>.Ok(ToResponse(request));
    }

    public async Task<List<FoundingRequestResponse>> ListMineAsync(CancellationToken ct)
    {
        var personId = context.PersonId!.Value;
        var requests = await db.FoundingRequests.Where(f => f.RequesterPersonId == personId).OrderByDescending(f => f.CreatedAt).ToListAsync(ct);
        return requests.Select(ToResponse).ToList();
    }

    public async Task<Outcome> CancelAsync(Guid requestId, CancellationToken ct)
    {
        var personId = context.PersonId!.Value;
        var now = clock.GetUtcNow();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (!await db.FoundingRequests.AnyAsync(f => f.Id == requestId && f.RequesterPersonId == personId, ct))
            return Outcome.NotFound;
        // Trava o consentimento antes do pedido, na mesma ordem da decisão (D-140).
        var consent = await db.Consents.SingleOrDefaultAsync(c => c.FoundingRequestId == requestId && c.RevokedAt == null && c.EndedAt == null, ct);
        if (consent is not null)
            await RowLocks.LockConsentAsync(db, consent.Id, exclusive: true, ct);
        var won = await db.FoundingRequests
            .Where(f => f.Id == requestId && f.RequesterPersonId == personId && f.Status == FoundingRequestStatus.Pending)
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.Status, FoundingRequestStatus.Cancelled).SetProperty(f => f.ClosedAt, now), ct);
        if (won == 0)
            return Outcome.Conflict();
        await db.Consents.Where(c => c.FoundingRequestId == requestId && c.RevokedAt == null && c.EndedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.EndedAt, now).SetProperty(c => c.EndCause, EndCause.RequestClosed), ct);
        audit.Record(AuditAction.FoundingCancelled, AuditTarget.FoundingRequest, requestId, null, AuditScope.Platform, personId);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Outcome.Done;
    }

    internal static FoundingRequestResponse ToResponse(FoundingRequest f) =>
        new(f.Id, f.ClubName, f.ClubCity, f.ClubChurch, f.Status, f.ExceptionRequested, f.CreatedAt);
}
