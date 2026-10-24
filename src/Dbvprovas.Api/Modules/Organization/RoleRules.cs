using Dbvprovas.Contracts;

namespace Dbvprovas.Api.Modules.Organization;

// Regras fixas dos cargos (RN-ORG-001, RN-ORG-002, RN-ORG-005, RN-ORG-017, D-170, D-154).
public static class RoleRules
{
    private static readonly RoleCode[] Board =
        [RoleCode.Director, RoleCode.AssociateDirector, RoleCode.Secretary, RoleCode.Treasurer, RoleCode.Chaplain, RoleCode.Pastor];

    private static readonly RoleCode[] Unique = [RoleCode.Director, RoleCode.Secretary, RoleCode.Treasurer];

    private static readonly RoleCode[] UnitScoped = [RoleCode.Counselor, RoleCode.AssociateCounselor];

    public static bool IsBoard(RoleCode role) => Board.Contains(role);

    public static bool IsUnique(RoleCode role) => Unique.Contains(role);

    public static bool RequiresUnit(RoleCode role) => UnitScoped.Contains(role);

    // Idade na data: diretoria 18; conselheiros e instrutor 16.
    public static int MinimumAge(RoleCode role) => IsBoard(role) ? 18 : 16;
}
