namespace Dbvprovas.Api.Modules.Tenancy;

// Contexto da requisição (D-121): a pessoa vem da sessão; o clube vem da rota, validado contra
// as participações. É o que o filtro do EF e o RLS usam (RN-TEN-001).
public sealed class ClubContext
{
    public Guid? PersonId { get; set; }
    public Guid? ClubId { get; set; }
}
