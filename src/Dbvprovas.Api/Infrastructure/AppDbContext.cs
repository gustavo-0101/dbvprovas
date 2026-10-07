using Dbvprovas.Api.Modules.Identity;
using Dbvprovas.Api.Modules.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Dbvprovas.Api.Infrastructure;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options, ClubContext clubContext) : DbContext(options)
{
    public DbSet<Club> Clubs => Set<Club>();
    public DbSet<Person> Persons => Set<Person>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Membership> Memberships => Set<Membership>();

    // O EF lê estas propriedades a cada consulta, como parâmetros dos filtros.
    private Guid? CurrentClubId => clubContext.ClubId;
    private Guid? CurrentPersonId => clubContext.PersonId;

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Club>(e =>
        {
            e.ToTable("clubs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.Name).HasColumnName("name").IsRequired();
            e.Property(x => x.Status).HasColumnName("status").HasConversion<string>().IsRequired();
            // RNF-TEN-001, D-124
            e.HasQueryFilter(c => c.Id == CurrentClubId
                || (CurrentClubId == null && Memberships.Any(m => m.ClubId == c.Id
                    && m.PersonId == CurrentPersonId && m.EndedAt == null)));
        });

        model.Entity<Person>(e =>
        {
            e.ToTable("persons");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.Name).HasColumnName("name").IsRequired();
            // Espelha a política persons_select do RLS (RNF-TEN-001).
            e.HasQueryFilter(p => p.Id == CurrentPersonId
                || Memberships.Any(m => m.PersonId == p.Id && m.ClubId == CurrentClubId));
        });

        model.Entity<Account>(e =>
        {
            e.ToTable("accounts");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.PersonId).HasColumnName("person_id");
            e.HasIndex(x => x.PersonId).IsUnique(); // uma conta por pessoa (RN-ID-001)
            e.HasOne<Person>().WithMany().HasForeignKey(x => x.PersonId);
        });

        model.Entity<Membership>(e =>
        {
            e.ToTable("memberships");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.ClubId).HasColumnName("club_id");
            e.Property(x => x.PersonId).HasColumnName("person_id");
            e.Property(x => x.StartedAt).HasColumnName("started_at");
            e.Property(x => x.EndedAt).HasColumnName("ended_at");
            e.HasIndex(x => x.ClubId);
            e.HasIndex(x => x.PersonId);
            e.HasOne<Club>().WithMany().HasForeignKey(x => x.ClubId);
            e.HasOne<Person>().WithMany().HasForeignKey(x => x.PersonId);
            // Espelha a política memberships_select do RLS (RNF-TEN-001).
            e.HasQueryFilter(m => m.ClubId == CurrentClubId || m.PersonId == CurrentPersonId);
        });
    }
}
