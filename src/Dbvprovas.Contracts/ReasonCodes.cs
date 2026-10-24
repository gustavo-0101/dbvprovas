using System.Text.Json.Serialization;

namespace Dbvprovas.Contracts;

// Motivos obrigatórios por categoria, sem texto livre (D-160).
[JsonConverter(typeof(StrictEnumConverter<ReasonCode>))]
public enum ReasonCode
{
    IncompleteInformation,
    DuplicateRequest,
    ConditionsNotMet,
    NewFoundingAfterPrevious,
    TemporaryLeave,
    AdministrativeReview,
    RemovalRequested,
    AdministrativeClosure,
    ReturnAfterLeave,
    SuspensionCorrection,
    DirectorUnavailable,
    DirectorshipRegularization,
    ClubSupportRequest,
    RegistrationInconsistency,
}

[JsonConverter(typeof(StrictEnumConverter<ReasonOperation>))]
public enum ReasonOperation
{
    RejectRequest,
    FoundingException,
    Suspend,
    Remove,
    Reactivate,
    ReplaceDirector,
    AdminAccessRequest,
}

public static class ReasonCodes
{
    private static readonly Dictionary<ReasonOperation, ReasonCode[]> Table = new()
    {
        [ReasonOperation.RejectRequest] = [ReasonCode.IncompleteInformation, ReasonCode.DuplicateRequest, ReasonCode.ConditionsNotMet],
        [ReasonOperation.FoundingException] = [ReasonCode.NewFoundingAfterPrevious],
        [ReasonOperation.Suspend] = [ReasonCode.TemporaryLeave, ReasonCode.AdministrativeReview],
        [ReasonOperation.Remove] = [ReasonCode.RemovalRequested, ReasonCode.AdministrativeClosure],
        [ReasonOperation.Reactivate] = [ReasonCode.ReturnAfterLeave, ReasonCode.SuspensionCorrection],
        [ReasonOperation.ReplaceDirector] = [ReasonCode.DirectorUnavailable, ReasonCode.DirectorshipRegularization],
        [ReasonOperation.AdminAccessRequest] = [ReasonCode.ClubSupportRequest, ReasonCode.RegistrationInconsistency],
    };

    public static IReadOnlyList<ReasonCode> AllowedFor(ReasonOperation operation) => Table[operation];

    public static bool IsAllowed(ReasonOperation operation, ReasonCode? code) =>
        code is { } value && Table[operation].Contains(value);
}
