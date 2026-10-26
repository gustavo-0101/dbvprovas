using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dbvprovas.Contracts;

// Enum só por nome (D-100): número no JSON é recusado. Texto com número é barrado na fronteira (Enum.IsDefined) e no banco.
public sealed class StrictEnumConverter<TEnum> : JsonConverterFactory where TEnum : struct, Enum
{
    private readonly JsonStringEnumConverter<TEnum> _inner = new(namingPolicy: null, allowIntegerValues: false);

    public override bool CanConvert(Type typeToConvert) => _inner.CanConvert(typeToConvert);

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options) => _inner.CreateConverter(typeToConvert, options);
}

// Cargos fixos da plataforma, iguais em todos os clubes (RN-ORG-001).
[JsonConverter(typeof(StrictEnumConverter<RoleCode>))]
public enum RoleCode
{
    Director,
    AssociateDirector,
    Secretary,
    Treasurer,
    Chaplain,
    Pastor,
    Counselor,
    AssociateCounselor,
    Instructor,
}

// Tipo da participação no clube.
[JsonConverter(typeof(StrictEnumConverter<MembershipKind>))]
public enum MembershipKind
{
    Leader,
}

[JsonConverter(typeof(StrictEnumConverter<MembershipStatus>))]
public enum MembershipStatus
{
    Active,
    Suspended,
    Ended,
}

[JsonConverter(typeof(StrictEnumConverter<Sex>))]
public enum Sex
{
    Female,
    Male,
}

[JsonConverter(typeof(StrictEnumConverter<UnitStatus>))]
public enum UnitStatus
{
    Active,
    Inactive,
}

[JsonConverter(typeof(StrictEnumConverter<FoundingRequestStatus>))]
public enum FoundingRequestStatus
{
    Pending,
    Approved,
    Rejected,
    Cancelled,
}

[JsonConverter(typeof(StrictEnumConverter<JoinRequestStatus>))]
public enum JoinRequestStatus
{
    Queued,
    Approved,
    Rejected,
    Expired,
    Cancelled,
}

[JsonConverter(typeof(StrictEnumConverter<JoinPath>))]
public enum JoinPath
{
    Search,
    Invite,
}

[JsonConverter(typeof(StrictEnumConverter<NominationStatus>))]
public enum NominationStatus
{
    Pending,
    Accepted,
    Declined,
    Cancelled,
    Expired,
}

[JsonConverter(typeof(StrictEnumConverter<AdminAccessStatus>))]
public enum AdminAccessStatus
{
    Pending,
    Authorized,
    Denied,
    Expired,
    Revoked,
}

// Conjuntos de dados que o admin pode pedir (RN-AUD-003).
[JsonConverter(typeof(StrictEnumConverter<AdminDataField>))]
public enum AdminDataField
{
    AdultProfile,
    Email,
    Memberships,
    Consents,
    JoinRequests,
}

[JsonConverter(typeof(StrictEnumConverter<ConsentKind>))]
public enum ConsentKind
{
    OwnParticipation,
}

// Causa de um encerramento automático, gravada na entidade e na auditoria (RN-AUD-002).
[JsonConverter(typeof(StrictEnumConverter<EndCause>))]
public enum EndCause
{
    RemovedByBoard,
    MembershipSuspended,
    MembershipEnded,
    ConsentRevoked,
    BecameDirector,
    DirectorTransferred,
    DirectorReplaced,
    Left,
    FoundedNewClub,
    Expired,
    RequestClosed,
}

[JsonConverter(typeof(StrictEnumConverter<AuditScope>))]
public enum AuditScope
{
    Club,
    Platform,
}

[JsonConverter(typeof(StrictEnumConverter<AuditTarget>))]
public enum AuditTarget
{
    Club,
    FoundingRequest,
    JoinRequest,
    Membership,
    RoleAssignment,
    DirectorNomination,
    Consent,
    AdminAccessRequest,
    Person,
}

// Eventos auditados no R1 (RN-AUD-001, CA-AUD-010).
[JsonConverter(typeof(StrictEnumConverter<AuditAction>))]
public enum AuditAction
{
    FoundingRequested,
    FoundingCancelled,
    FoundingApproved,
    FoundingRejected,
    JoinRequested,
    JoinCancelled,
    JoinApproved,
    JoinRejected,
    JoinExpired,
    RoleAssigned,
    RoleEnded,
    DirectorNominated,
    DirectorNominationCancelled,
    DirectorNominationDeclined,
    DirectorChanged,
    DirectorAppointed,
    DirectorReplaced,
    MembershipSuspended,
    MembershipReactivated,
    MembershipRemoved,
    MembershipLeft,
    MembershipEnded,
    ConsentGranted,
    ConsentRevoked,
    AdminAccessRequested,
    AdminAccessAuthorized,
    AdminAccessDenied,
    AdminAccessRevoked,
    AdminPersonalDataRead,
}

// Eventos entregues na caixa de avisos do R1 (RN-NTF-002, D-145).
[JsonConverter(typeof(StrictEnumConverter<NotificationEvent>))]
public enum NotificationEvent
{
    FoundingApproved,
    FoundingRejected,
    JoinQueued,
    JoinExpiringSoon,
    JoinApproved,
    JoinRejected,
    JoinExpired,
    DirectorNominated,
    DirectorChanged,
    ConsentRevoked,
    AdminAccessRequested,
    AdminAccessAuthorized,
    AdminAccessDenied,
    AdminAccessRevoked,
}

// Únicos e-mails do R1: a resposta ao pedido de fundação (RN-NTF-001).
[JsonConverter(typeof(StrictEnumConverter<EmailTemplate>))]
public enum EmailTemplate
{
    FoundingApproved,
    FoundingRejected,
}
