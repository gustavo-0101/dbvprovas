namespace Dbvprovas.Contracts;

// Códigos estáveis das respostas 400, 403 e 409, na extensão "code" do ProblemDetails (D-125). Nunca levam
// dado pessoal (PC-03). Cada módulo acrescenta os seus num arquivo próprio desta classe parcial.
public static partial class ProblemCodes
{
    public const string Extension = "code";

    public const string ValidationText = "validation.text";
    public const string ValidationDate = "validation.date";
    public const string ReasonRequired = "reason.required";
    public const string PermissionDenied = "permission.denied";
    public const string SelfAction = "self.action";
    public const string StateConflict = "state.conflict";
    public const string ProfileIncomplete = "profile.incomplete";
    public const string AgeBelowMinimum = "age.below_minimum";
    public const string ConsentRequired = "consent.required";
    public const string ConsentOutdated = "consent.outdated";
    public const string PendingRequestExists = "request.pending_exists";
}
