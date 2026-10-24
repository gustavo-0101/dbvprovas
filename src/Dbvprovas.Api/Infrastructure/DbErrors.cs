using Npgsql;

namespace Dbvprovas.Api.Infrastructure;

// Violação de um índice único do R1, pelo nome (DbConstraints). A transação fica abortada: quem chama devolve 409 sem gravar mais nada.
public static class DbErrors
{
    public static bool IsUniqueViolation(Exception exception, string constraint) =>
        Find(exception) is { SqlState: PostgresErrorCodes.UniqueViolation } error && error.ConstraintName == constraint;

    private static PostgresException? Find(Exception? exception)
    {
        for (; exception is not null; exception = exception.InnerException)
        {
            if (exception is PostgresException postgres)
                return postgres;
        }

        return null;
    }
}
