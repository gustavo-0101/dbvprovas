using Dbvprovas.Api.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dbvprovas.Api.Tests.Infrastructure;

// RNF-TEN-001
public static class DbScope
{
    public static ServiceProvider Create(PostgresFixture db)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:App"] = db.AppConnectionString })
            .Build();
        return new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddLogging()
            .AddDatabase()
            .BuildServiceProvider();
    }
}
