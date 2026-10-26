using Dbvprovas.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Dbvprovas.Api.Infrastructure;

// 400, 403 e 409 com ProblemDetails e só o código estável na extensão "code" (D-125, PC-03).
public static class ApiProblems
{
    public static ProblemHttpResult For(OutcomeKind kind, string code) => kind switch
    {
        OutcomeKind.Forbidden => Problem(StatusCodes.Status403Forbidden, code),
        OutcomeKind.Conflict => Problem(StatusCodes.Status409Conflict, code),
        OutcomeKind.Invalid => Problem(StatusCodes.Status400BadRequest, code),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public static ProblemHttpResult Problem(int status, string code) =>
        TypedResults.Problem(statusCode: status, extensions: new Dictionary<string, object?> { [ProblemCodes.Extension] = code });
}
