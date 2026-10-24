using Dbvprovas.Api.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dbvprovas.Api.Tests.Infrastructure;

// RNF-TEN-001
public static class DbScope
{
    public static ServiceProvider Create(PostgresFixture db, int? maxPoolSize = null)
    {
        var connectionString = maxPoolSize is { } size
            ? new Npgsql.NpgsqlConnectionStringBuilder(db.AppConnectionString) { MaxPoolSize = size }.ConnectionString
            : db.AppConnectionString;
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:App"] = connectionString })
            .Build();
        return new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddLogging()
            .AddDatabase()
            .AddR1Core()
            .BuildServiceProvider();
    }
}
