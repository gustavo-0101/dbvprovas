using Dbvprovas.Api.Tests.Infrastructure;

// Um Postgres 18 por execução, com o esquema e o seed fictício (D-098).
[assembly: AssemblyFixture(typeof(PostgresFixture))]
