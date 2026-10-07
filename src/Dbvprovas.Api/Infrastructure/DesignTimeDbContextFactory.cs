using Dbvprovas.Api.Modules.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Dbvprovas.Api.Infrastructure;

// Usado só pelo dotnet-ef ao gerar migrations; não abre conexão.
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql().Options, new ClubContext());
}
