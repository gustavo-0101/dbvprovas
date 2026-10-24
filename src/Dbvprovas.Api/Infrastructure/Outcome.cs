using Microsoft.AspNetCore.Http.HttpResults;
using Dbvprovas.Contracts;

namespace Dbvprovas.Api.Infrastructure;

public enum OutcomeKind
{
    Done,
    NotFound,
    Forbidden,
    Conflict,
    Invalid,
}

// Resultado de uma operação de domínio, traduzido em HTTP pelo endpoint (D-117, D-125).
public readonly record struct Outcome(OutcomeKind Kind, string? Code = null)
{
    public static Outcome Done => new(OutcomeKind.Done);

    public static Outcome NotFound => new(OutcomeKind.NotFound);

    public static Outcome Forbidden(string code = ProblemCodes.PermissionDenied) => new(OutcomeKind.Forbidden, code);

    public static Outcome Conflict(string code = ProblemCodes.StateConflict) => new(OutcomeKind.Conflict, code);

    public static Outcome Invalid(string code) => new(OutcomeKind.Invalid, code);

    public Results<NoContent, NotFound, ProblemHttpResult> ToHttp() => Kind switch
    {
        OutcomeKind.Done => TypedResults.NoContent(),
        OutcomeKind.NotFound => TypedResults.NotFound(),
        _ => ApiProblems.For(Kind, Code!),
    };
}

// Sucesso sempre por Ok(value); a conversão implícita de Outcome serve só para os erros.
public readonly record struct Outcome<T>(OutcomeKind Kind, T? Value, string? Code = null)
{
    public static Outcome<T> Ok(T value) => new(OutcomeKind.Done, value);

    public static implicit operator Outcome<T>(Outcome outcome) => new(outcome.Kind, default, outcome.Code);

    public Results<Ok<T>, NotFound, ProblemHttpResult> ToHttp() => Kind switch
    {
        OutcomeKind.Done => TypedResults.Ok(Value!),
        OutcomeKind.NotFound => TypedResults.NotFound(),
        _ => ApiProblems.For(Kind, Code!),
    };
}
