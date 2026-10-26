namespace Dbvprovas.Api.Infrastructure;

// Nomes dos índices únicos do R1. Uma violação de um deles vira 409 (D-140), conferida por DbErrors.IsUniqueViolation.
public static class DbConstraints
{
    public const string OneActiveMembershipPerPerson = "ux_memberships_one_active_per_person";
    public const string UniqueClubRoles = "ux_role_assignments_unique_club_roles";
    public const string OneAssignmentPerRole = "ux_role_assignments_one_per_membership_role";
    public const string OnePendingNomination = "ux_director_nominations_one_pending";
    public const string OnePendingFounding = "ux_founding_requests_one_pending_per_person";
    public const string OneActiveConsentPerClub = "ux_consents_one_active_per_club";
    public const string InviteCode = "ux_clubs_invite_code";
    public const string GoogleSubject = "ux_accounts_google_subject";
    public const string NotificationOnce = "ux_notifications_once";
    public const string EmailOnce = "ux_email_outbox_once";
    public const string SessionToken = "ux_sessions_token_hash";
    public const string SessionNonce = "ux_sessions_nonce_hash";

    public static readonly IReadOnlyList<string> All =
    [
        OneActiveMembershipPerPerson, UniqueClubRoles, OneAssignmentPerRole, OnePendingNomination, OnePendingFounding,
        OneActiveConsentPerClub, InviteCode, GoogleSubject, NotificationOnce, EmailOnce, SessionToken, SessionNonce,
    ];
}
