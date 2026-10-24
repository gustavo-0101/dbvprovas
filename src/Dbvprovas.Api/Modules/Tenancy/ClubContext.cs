namespace Dbvprovas.Api.Modules.Tenancy;

// Contexto da requisição (D-121): a conta vem da sessão; a pessoa e o papel de admin, da conta, a cada
// requisição (PC-09); o clube vem da rota, validado contra as participações, ou da troca explícita dos
// caminhos específicos (RNF-TEN-006). É o que o filtro do EF e o RLS usam (RN-TEN-001).
public sealed class ClubContext
{
    public Guid? AccountId { get; set; }
    public Guid? PersonId { get; set; }
    public Guid? ClubId { get; set; }
    public bool IsPlatformAdmin { get; set; }
}
