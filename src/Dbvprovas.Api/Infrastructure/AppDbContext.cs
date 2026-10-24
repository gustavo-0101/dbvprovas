using Dbvprovas.Api.Modules.Audit;
using Dbvprovas.Api.Modules.Audit.AdminAccess;
using Dbvprovas.Api.Modules.Identity;
using Dbvprovas.Api.Modules.Joining;
using Dbvprovas.Api.Modules.Notifications;
using Dbvprovas.Api.Modules.Organization;
using Dbvprovas.Api.Modules.Privacy;
using Dbvprovas.Api.Modules.Tenancy;
using Dbvprovas.Api.Modules.Tenancy.Founding;
using Dbvprovas.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Dbvprovas.Api.Infrastructure;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options, ClubContext clubContext) : DbContext(options)
{
    public DbSet<Club> Clubs => Set<Club>();
    public DbSet<Person> Persons => Set<Person>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<RoleAssignment> RoleAssignments => Set<RoleAssignment>();
    public DbSet<DirectorNomination> DirectorNominations => Set<DirectorNomination>();
    public DbSet<FoundingRequest> FoundingRequests => Set<FoundingRequest>();
    public DbSet<FoundingRecord> FoundingRecords => Set<FoundingRecord>();
    public DbSet<JoinRequest> JoinRequests => Set<JoinRequest>();
    public DbSet<Consent> Consents => Set<Consent>();
    public DbSet<AdminAccessRequest> AdminAccessRequests => Set<AdminAccessRequest>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<EmailOutboxMessage> EmailOutbox => Set<EmailOutboxMessage>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<ClubDirectoryEntry> ClubDirectory => Set<ClubDirectoryEntry>();
    public DbSet<AdminClubOverview> AdminClubOverview => Set<AdminClubOverview>();

    // O EF lê estas propriedades a cada consulta, como parâmetros dos filtros (RNF-TEN-001).
    private Guid? CurrentClubId => clubContext.ClubId;
    private Guid? CurrentPersonId => clubContext.PersonId;
    private bool CurrentIsPlatformAdmin => clubContext.IsPlatformAdmin;

    protected override void OnModelCreating(ModelBuilder model)
    {
        ConfigureTenancy(model);
        ConfigureIdentity(model);
        ConfigureOrganization(model);
        ConfigureJoiningAndPrivacy(model);
        ConfigureAuditAndNotifications(model);
    }

    private void ConfigureTenancy(ModelBuilder model)
    {
        model.Entity<Club>(e =>
        {
            e.ToTable("clubs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.Name).HasColumnName("name").IsRequired();
            e.Property(x => x.City).HasColumnName("city").IsRequired();
            e.Property(x => x.Church).HasColumnName("church");
            e.Property(x => x.InviteCode).HasColumnName("invite_code").IsRequired();
            e.Property(x => x.Status).HasColumnName("status").HasConversion<string>().IsRequired();
            e.Property(x => x.FoundingRequestId).HasColumnName("founding_request_id");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasIndex(x => x.InviteCode, DbConstraints.InviteCode).IsUnique();
            // RNF-TEN-001, D-124: sem clube, só os clubes das participações próprias ativas (CA-TEN-001 R1).
            e.HasQueryFilter(c => c.Id == CurrentClubId
                || (CurrentClubId == null && Memberships.Any(m => m.ClubId == c.Id
                    && m.PersonId == CurrentPersonId && m.Status == MembershipStatus.Active)));
        });

        model.Entity<Membership>(e =>
        {
            e.ToTable("memberships", t =>
            {
                t.HasCheckConstraint("ck_memberships_ended", "(status = 'Ended') = (ended_at IS NOT NULL)");
                t.HasCheckConstraint("ck_memberships_suspended", "(status = 'Suspended') = (suspended_at IS NOT NULL)");
            });
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.ClubId).HasColumnName("club_id");
            e.Property(x => x.PersonId).HasColumnName("person_id");
            e.Property(x => x.Kind).HasColumnName("kind").HasConversion<string>().IsRequired();
            e.Property(x => x.Status).HasColumnName("status").HasConversion<string>().IsRequired();
            e.Property(x => x.StartedAt).HasColumnName("started_at");
            e.Property(x => x.SuspendedAt).HasColumnName("suspended_at");
            e.Property(x => x.EndedAt).HasColumnName("ended_at");
            e.Property(x => x.StatusReason).HasColumnName("status_reason").HasConversion<string>();
            e.Property(x => x.StatusCause).HasColumnName("status_cause").HasConversion<string>();
            e.HasIndex(x => x.ClubId);
            e.HasIndex(x => x.PersonId);
            // Chave (clube, id): quem referencia a participação fica no mesmo clube, também para o banco (RN-TEN-001).
            e.HasAlternateKey(x => new { x.ClubId, x.Id });
            // RN-TEN-006: uma participação ativa por pessoa, garantida pelo banco também sob concorrência.
            e.HasIndex(x => x.PersonId, DbConstraints.OneActiveMembershipPerPerson).IsUnique().HasFilter("status = 'Active'");
            e.HasOne<Club>().WithMany().HasForeignKey(x => x.ClubId);
            e.HasOne<Person>().WithMany().HasForeignKey(x => x.PersonId);
            // Espelha a política memberships_select do RLS (RNF-TEN-001).
            e.HasQueryFilter(m => m.ClubId == CurrentClubId || m.PersonId == CurrentPersonId);
        });

        model.Entity<FoundingRequest>(e =>
        {
            e.ToTable("founding_requests");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.RequesterPersonId).HasColumnName("requester_person_id");
            e.Property(x => x.RequesterAccountId).HasColumnName("requester_account_id");
            e.Property(x => x.ClubName).HasColumnName("club_name").IsRequired();
            e.Property(x => x.ClubCity).HasColumnName("club_city").IsRequired();
            e.Property(x => x.ClubChurch).HasColumnName("club_church");
            e.Property(x => x.ProposedClubId).HasColumnName("proposed_club_id");
            e.Property(x => x.Status).HasColumnName("status").HasConversion<string>().IsRequired();
            e.Property(x => x.ExceptionRequested).HasColumnName("exception_requested");
            e.Property(x => x.ExceptionGranted).HasColumnName("exception_granted");
            e.Property(x => x.ReasonCode).HasColumnName("reason_code").HasConversion<string>();
            e.Property(x => x.DecidedByPersonId).HasColumnName("decided_by_person_id");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.ClosedAt).HasColumnName("closed_at");
            e.HasIndex(x => x.ProposedClubId).IsUnique();
            e.HasIndex(x => x.RequesterPersonId, DbConstraints.OnePendingFounding).IsUnique().HasFilter("status = 'Pending'");
            e.HasOne<Person>().WithMany().HasForeignKey(x => x.RequesterPersonId);
            e.HasOne<Account>().WithMany().HasForeignKey(x => x.RequesterAccountId);
            e.HasQueryFilter(f => f.RequesterPersonId == CurrentPersonId || CurrentIsPlatformAdmin);
        });

        model.Entity<FoundingRecord>(e =>
        {
            e.ToTable("founding_records");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.PersonId).HasColumnName("person_id");
            e.Property(x => x.ClubId).HasColumnName("club_id");
            e.Property(x => x.ApprovedAt).HasColumnName("approved_at");
            e.HasIndex(x => x.PersonId);
            e.HasOne<Person>().WithMany().HasForeignKey(x => x.PersonId);
            e.HasQueryFilter(r => r.PersonId == CurrentPersonId || CurrentIsPlatformAdmin);
        });

        model.Entity<ClubDirectoryEntry>(e =>
        {
            e.HasNoKey();
            e.ToView("club_directory");
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Name).HasColumnName("name");
            e.Property(x => x.City).HasColumnName("city");
            e.Property(x => x.Church).HasColumnName("church");
        });

        model.Entity<AdminClubOverview>(e =>
        {
            e.HasNoKey();
            e.ToView("admin_club_overview");
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Name).HasColumnName("name");
            e.Property(x => x.City).HasColumnName("city");
            e.Property(x => x.HasDirector).HasColumnName("has_director");
        });
    }

    private void ConfigureIdentity(ModelBuilder model)
    {
        model.Entity<Person>(e =>
        {
            e.ToTable("persons", t => t.HasCheckConstraint("ck_persons_sex", "sex IS NULL OR sex IN ('Female', 'Male')"));
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.Name).HasColumnName("name");
            e.Property(x => x.BirthDate).HasColumnName("birth_date");
            e.Property(x => x.Sex).HasColumnName("sex").HasConversion<string>();
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            // Espelha a política persons_select do RLS (RNF-TEN-001, RN-JOIN-004, RN-AUD-006).
            e.HasQueryFilter(p => p.Id == CurrentPersonId
                || Memberships.Any(m => m.PersonId == p.Id && m.ClubId == CurrentClubId)
                || JoinRequests.Any(j => j.CandidatePersonId == p.Id && j.ClubId == CurrentClubId)
                || (CurrentIsPlatformAdmin && FoundingRequests.Any(f => f.RequesterPersonId == p.Id && f.Status == FoundingRequestStatus.Pending)));
        });

        model.Entity<Account>(e =>
        {
            e.ToTable("accounts");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.PersonId).HasColumnName("person_id");
            e.Property(x => x.GoogleSubject).HasColumnName("google_subject").IsRequired();
            e.Property(x => x.Email).HasColumnName("email");
            e.Property(x => x.IsPlatformAdmin).HasColumnName("is_platform_admin");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasIndex(x => x.PersonId).IsUnique(); // uma conta por pessoa (RN-ID-001)
            e.HasIndex(x => x.GoogleSubject, DbConstraints.GoogleSubject).IsUnique();
            e.HasOne<Person>().WithMany().HasForeignKey(x => x.PersonId);
        });

        // Sem filtro nem RLS, como as contas: a sessão é achada pelo hash antes de haver pessoa no contexto (RF-ID-006).
        model.Entity<Session>(e =>
        {
            e.ToTable("sessions", t => t.HasCheckConstraint("ck_sessions_target", "target IN ('Web', 'App')"));
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.AccountId).HasColumnName("account_id");
            e.Property(x => x.TokenHash).HasColumnName("token_hash").IsRequired();
            e.Property(x => x.NonceHash).HasColumnName("nonce_hash").IsRequired();
            e.Property(x => x.Target).HasColumnName("target").HasConversion<string>().IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            e.Property(x => x.RevokedAt).HasColumnName("revoked_at");
            e.HasIndex(x => x.TokenHash, DbConstraints.SessionToken).IsUnique();
            e.HasIndex(x => x.NonceHash, DbConstraints.SessionNonce).IsUnique();
            e.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId);
        });
    }

    private void ConfigureOrganization(ModelBuilder model)
    {
        model.Entity<Unit>(e =>
        {
            e.ToTable("units", t =>
            {
                // RN-ORG-013, RN-ORG-018: faixa no ano entre 10 e 15.
                t.HasCheckConstraint("ck_units_age_range", "min_age <= max_age AND min_age BETWEEN 10 AND 15 AND max_age BETWEEN 10 AND 15");
                t.HasCheckConstraint("ck_units_sex", "sex IN ('Female', 'Male')");
            });
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.ClubId).HasColumnName("club_id");
            e.Property(x => x.Name).HasColumnName("name").IsRequired();
            e.Property(x => x.Sex).HasColumnName("sex").HasConversion<string>().IsRequired();
            e.Property(x => x.MinAge).HasColumnName("min_age");
            e.Property(x => x.MaxAge).HasColumnName("max_age");
            e.Property(x => x.Status).HasColumnName("status").HasConversion<string>().IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasIndex(x => x.ClubId);
            e.HasOne<Club>().WithMany().HasForeignKey(x => x.ClubId);
            // Chave (clube, id): quem referencia a unidade fica no mesmo clube, também para o banco (RN-TEN-001).
            e.HasAlternateKey(x => new { x.ClubId, x.Id });
            e.HasQueryFilter(u => u.ClubId == CurrentClubId);
        });

        model.Entity<RoleAssignment>(e =>
        {
            e.ToTable("role_assignments", t =>
            {
                t.HasCheckConstraint("ck_role_assignments_unit", "(role IN ('Counselor', 'AssociateCounselor')) = (unit_id IS NOT NULL)");
                // Só os nove cargos fixos (RN-ORG-001).
                t.HasCheckConstraint("ck_role_assignments_role", "role IN ('Director', 'AssociateDirector', 'Secretary', 'Treasurer', "
                    + "'Chaplain', 'Pastor', 'Counselor', 'AssociateCounselor', 'Instructor')");
            });
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.ClubId).HasColumnName("club_id");
            e.Property(x => x.MembershipId).HasColumnName("membership_id");
            e.Property(x => x.Role).HasColumnName("role").HasConversion<string>().IsRequired();
            e.Property(x => x.UnitId).HasColumnName("unit_id");
            e.Property(x => x.AssignedByPersonId).HasColumnName("assigned_by_person_id");
            e.Property(x => x.StartedAt).HasColumnName("started_at");
            e.Property(x => x.EndedAt).HasColumnName("ended_at");
            e.Property(x => x.EndedByPersonId).HasColumnName("ended_by_person_id");
            e.Property(x => x.EndCause).HasColumnName("end_cause").HasConversion<string>();
            e.HasIndex(x => x.ClubId);
            // D-170: diretor, secretário e tesoureiro únicos por clube; nenhuma pessoa com o mesmo cargo duas vezes.
            e.HasIndex(x => new { x.ClubId, x.Role }, DbConstraints.UniqueClubRoles).IsUnique()
                .HasFilter("ended_at IS NULL AND role IN ('Director', 'Secretary', 'Treasurer')");
            e.HasIndex(x => new { x.MembershipId, x.Role }, DbConstraints.OneAssignmentPerRole).IsUnique().HasFilter("ended_at IS NULL");
            e.HasOne<Club>().WithMany().HasForeignKey(x => x.ClubId);
            // Participação e unidade do mesmo clube da atribuição (RN-TEN-001); unidade nula não é conferida.
            e.HasOne<Membership>().WithMany().HasForeignKey(x => new { x.ClubId, x.MembershipId }).HasPrincipalKey(m => new { m.ClubId, m.Id });
            e.HasOne<Unit>().WithMany().HasForeignKey(x => new { x.ClubId, x.UnitId }).HasPrincipalKey(u => new { u.ClubId, u.Id });
            // Espelha role_assignments_select: o clube, ou os próprios cargos sem clube (para "quem sou eu").
            e.HasQueryFilter(r => r.ClubId == CurrentClubId
                || Memberships.Any(m => m.Id == r.MembershipId && m.PersonId == CurrentPersonId));
        });

        model.Entity<DirectorNomination>(e =>
        {
            e.ToTable("director_nominations");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.ClubId).HasColumnName("club_id");
            e.Property(x => x.NominatorMembershipId).HasColumnName("nominator_membership_id");
            e.Property(x => x.NomineeMembershipId).HasColumnName("nominee_membership_id");
            e.Property(x => x.Status).HasColumnName("status").HasConversion<string>().IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            e.Property(x => x.ClosedAt).HasColumnName("closed_at");
            e.HasIndex(x => x.ClubId, DbConstraints.OnePendingNomination).IsUnique().HasFilter("status = 'Pending'"); // D-163
            e.HasOne<Club>().WithMany().HasForeignKey(x => x.ClubId);
            // As duas participações são do clube da indicação (RN-TEN-001).
            e.HasOne<Membership>().WithMany().HasForeignKey(x => new { x.ClubId, x.NominatorMembershipId }).HasPrincipalKey(m => new { m.ClubId, m.Id });
            e.HasOne<Membership>().WithMany().HasForeignKey(x => new { x.ClubId, x.NomineeMembershipId }).HasPrincipalKey(m => new { m.ClubId, m.Id });
            e.HasQueryFilter(n => n.ClubId == CurrentClubId);
        });
    }

    private void ConfigureJoiningAndPrivacy(ModelBuilder model)
    {
        model.Entity<JoinRequest>(e =>
        {
            e.ToTable("join_requests");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.ClubId).HasColumnName("club_id");
            e.Property(x => x.CandidatePersonId).HasColumnName("candidate_person_id");
            e.Property(x => x.CreatedByAccountId).HasColumnName("created_by_account_id");
            e.Property(x => x.Kind).HasColumnName("kind").HasConversion<string>().IsRequired();
            e.Property(x => x.Path).HasColumnName("path").HasConversion<string>().IsRequired();
            e.Property(x => x.Status).HasColumnName("status").HasConversion<string>().IsRequired();
            e.Property(x => x.ConsentId).HasColumnName("consent_id");
            e.Property(x => x.QueuedAt).HasColumnName("queued_at");
            e.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            e.Property(x => x.ReminderSentAt).HasColumnName("reminder_sent_at");
            e.Property(x => x.DecidedByPersonId).HasColumnName("decided_by_person_id");
            e.Property(x => x.ReasonCode).HasColumnName("reason_code").HasConversion<string>();
            e.Property(x => x.CloseCause).HasColumnName("close_cause").HasConversion<string>();
            e.Property(x => x.ClosedAt).HasColumnName("closed_at");
            e.HasIndex(x => new { x.ClubId, x.Status });
            e.HasIndex(x => x.CandidatePersonId);
            e.HasOne<Club>().WithMany().HasForeignKey(x => x.ClubId);
            e.HasOne<Person>().WithMany().HasForeignKey(x => x.CandidatePersonId);
            e.HasOne<Account>().WithMany().HasForeignKey(x => x.CreatedByAccountId);
            e.HasOne<Consent>().WithMany().HasForeignKey(x => x.ConsentId);
            e.HasQueryFilter(j => j.ClubId == CurrentClubId || j.CandidatePersonId == CurrentPersonId);
        });

        model.Entity<Consent>(e =>
        {
            e.ToTable("consents", t => t.HasCheckConstraint("ck_consents_context", "club_id IS NOT NULL OR founding_request_id IS NOT NULL"));
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.PersonId).HasColumnName("person_id");
            e.Property(x => x.Kind).HasColumnName("kind").HasConversion<string>().IsRequired();
            e.Property(x => x.ClubId).HasColumnName("club_id");
            e.Property(x => x.FoundingRequestId).HasColumnName("founding_request_id");
            e.Property(x => x.TermVersion).HasColumnName("term_version").IsRequired();
            e.Property(x => x.ConsentedByAccountId).HasColumnName("consented_by_account_id");
            e.Property(x => x.GrantedAt).HasColumnName("granted_at");
            e.Property(x => x.RevokedAt).HasColumnName("revoked_at");
            e.Property(x => x.EndedAt).HasColumnName("ended_at");
            e.Property(x => x.EndCause).HasColumnName("end_cause").HasConversion<string>();
            e.HasIndex(x => x.PersonId);
            e.HasIndex(x => new { x.PersonId, x.ClubId }, DbConstraints.OneActiveConsentPerClub).IsUnique()
                .HasFilter("revoked_at IS NULL AND ended_at IS NULL AND club_id IS NOT NULL");
            e.HasOne<Person>().WithMany().HasForeignKey(x => x.PersonId);
            e.HasOne<Club>().WithMany().HasForeignKey(x => x.ClubId);
            e.HasOne<FoundingRequest>().WithMany().HasForeignKey(x => x.FoundingRequestId);
            e.HasQueryFilter(c => c.PersonId == CurrentPersonId
                || (c.ClubId != null && c.ClubId == CurrentClubId)
                || (c.ClubId == null && CurrentIsPlatformAdmin));
        });
    }

    private void ConfigureAuditAndNotifications(ModelBuilder model)
    {
        model.Entity<AuditEntry>(e =>
        {
            e.ToTable("audit_entries");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.ClubId).HasColumnName("club_id");
            e.Property(x => x.Scope).HasColumnName("scope").HasConversion<string>().IsRequired();
            e.Property(x => x.ActorPersonId).HasColumnName("actor_person_id");
            e.Property(x => x.Action).HasColumnName("action").HasConversion<string>().IsRequired();
            e.Property(x => x.TargetType).HasColumnName("target_type").HasConversion<string>().IsRequired();
            e.Property(x => x.TargetId).HasColumnName("target_id");
            e.Property(x => x.ReasonCode).HasColumnName("reason_code").HasConversion<string>();
            e.Property(x => x.Cause).HasColumnName("cause").HasConversion<string>();
            e.Property(x => x.AdminAccessRequestId).HasColumnName("admin_access_request_id");
            e.Property(x => x.Fields).HasColumnName("fields").IsRequired();
            e.Property(x => x.OccurredAt).HasColumnName("occurred_at");
            e.HasIndex(x => new { x.ClubId, x.OccurredAt });
            e.HasIndex(x => new { x.Scope, x.OccurredAt });
            // Sem chave estrangeira: a auditoria tem guarda própria de 3 anos (RN-PRV-010).
            e.HasQueryFilter(a => (a.ClubId != null && a.ClubId == CurrentClubId)
                || (a.Scope == AuditScope.Platform && CurrentIsPlatformAdmin));
        });

        model.Entity<AdminAccessRequest>(e =>
        {
            e.ToTable("admin_access_requests");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.ClubId).HasColumnName("club_id");
            e.Property(x => x.RequestedByPersonId).HasColumnName("requested_by_person_id");
            e.Property(x => x.ReasonCode).HasColumnName("reason_code").HasConversion<string>().IsRequired();
            e.Property(x => x.TargetMembershipIds).HasColumnName("target_membership_ids").IsRequired();
            e.Property(x => x.Fields).HasColumnName("fields").IsRequired();
            e.Property(x => x.Status).HasColumnName("status").HasConversion<string>().IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            e.Property(x => x.DecidedByPersonId).HasColumnName("decided_by_person_id");
            e.Property(x => x.DecidedAt).HasColumnName("decided_at");
            e.Property(x => x.ValidUntil).HasColumnName("valid_until");
            e.Property(x => x.RevokedByPersonId).HasColumnName("revoked_by_person_id");
            e.Property(x => x.RevokedAt).HasColumnName("revoked_at");
            e.HasIndex(x => x.ClubId);
            e.HasOne<Club>().WithMany().HasForeignKey(x => x.ClubId);
            e.HasQueryFilter(r => r.ClubId == CurrentClubId || CurrentIsPlatformAdmin);
        });

        model.Entity<Notification>(e =>
        {
            e.ToTable("notifications");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.RecipientPersonId).HasColumnName("recipient_person_id");
            e.Property(x => x.Event).HasColumnName("event").HasConversion<string>().IsRequired();
            e.Property(x => x.ClubId).HasColumnName("club_id");
            e.Property(x => x.ObjectId).HasColumnName("object_id");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.ReadAt).HasColumnName("read_at");
            e.HasIndex(x => new { x.RecipientPersonId, x.Event, x.ObjectId }, DbConstraints.NotificationOnce).IsUnique();
            e.HasOne<Person>().WithMany().HasForeignKey(x => x.RecipientPersonId);
            e.HasQueryFilter(n => n.RecipientPersonId == CurrentPersonId);
        });

        model.Entity<EmailOutboxMessage>(e =>
        {
            e.ToTable("email_outbox");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.RecipientAccountId).HasColumnName("recipient_account_id");
            e.Property(x => x.Template).HasColumnName("template").HasConversion<string>().IsRequired();
            e.Property(x => x.ObjectId).HasColumnName("object_id");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.SentAt).HasColumnName("sent_at");
            e.Property(x => x.Attempts).HasColumnName("attempts");
            e.Property(x => x.NextAttemptAt).HasColumnName("next_attempt_at");
            e.HasIndex(x => new { x.Template, x.ObjectId, x.RecipientAccountId }, DbConstraints.EmailOnce).IsUnique();
        });
    }
}
