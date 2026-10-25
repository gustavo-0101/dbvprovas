using Dbvprovas.Contracts;
using Dbvprovas.Ui.Text;

namespace Dbvprovas.Ui.Tests.Shared;

public sealed class LabelsTests
{
    // Toda opção dos contratos tem texto em pt-BR na tela; os motivos repetem a tabela da D-160.
    [Fact]
    public void Every_contract_value_has_a_label()
    {
        foreach (var value in Enum.GetValues<RoleCode>()) Assert.False(string.IsNullOrWhiteSpace(Labels.Of(value)));
        foreach (var value in Enum.GetValues<ReasonCode>()) Assert.False(string.IsNullOrWhiteSpace(Labels.Of(value)));
        foreach (var value in Enum.GetValues<Sex>()) Assert.False(string.IsNullOrWhiteSpace(Labels.Of(value)));
        foreach (var value in Enum.GetValues<MembershipStatus>()) Assert.False(string.IsNullOrWhiteSpace(Labels.Of(value)));
        foreach (var value in Enum.GetValues<UnitStatus>()) Assert.False(string.IsNullOrWhiteSpace(Labels.Of(value)));
        foreach (var value in Enum.GetValues<FoundingRequestStatus>()) Assert.False(string.IsNullOrWhiteSpace(Labels.Of(value)));
        foreach (var value in Enum.GetValues<JoinRequestStatus>()) Assert.False(string.IsNullOrWhiteSpace(Labels.Of(value)));
        foreach (var value in Enum.GetValues<NominationStatus>()) Assert.False(string.IsNullOrWhiteSpace(Labels.Of(value)));
        foreach (var value in Enum.GetValues<AdminAccessStatus>()) Assert.False(string.IsNullOrWhiteSpace(Labels.Of(value)));
        foreach (var value in Enum.GetValues<AdminDataField>()) Assert.False(string.IsNullOrWhiteSpace(Labels.Of(value)));
        foreach (var value in Enum.GetValues<AuditAction>()) Assert.False(string.IsNullOrWhiteSpace(Labels.Of(value)));
        foreach (var value in Enum.GetValues<EndCause>()) Assert.False(string.IsNullOrWhiteSpace(Labels.Of(value)));
        foreach (var value in Enum.GetValues<NotificationEvent>()) Assert.False(string.IsNullOrWhiteSpace(Labels.Of(value)));
        Assert.Equal("Afastamento temporário", Labels.Of(ReasonCode.TemporaryLeave));
        Assert.Equal("Não foi possível concluir. Tente de novo.", Labels.Problem("codigo.desconhecido"));
    }

    // RN-PRV-002: a versão do termo aparece com texto em pt-BR; versão desconhecida não mostra o ID técnico.
    [Fact]
    public void Term_version_label_names_the_current_version_and_hides_unknown_ones()
    {
        Assert.Equal("Participação própria do adulto, versão 1", Labels.TermVersion(ConsentTerms.AdultOwnParticipationVersion));
        Assert.Equal("Versão não identificada", Labels.TermVersion("adult-own-participation-old"));
        Assert.Equal("Versão não identificada", Labels.TermVersion(null));
    }

    // D-160: os 14 motivos têm exatamente o texto da tabela confirmada; um motivo novo nos contratos obriga a revisar a tabela.
    [Fact]
    public void Reason_labels_match_the_confirmed_table()
    {
        var expected = new Dictionary<ReasonCode, string>
        {
            [ReasonCode.IncompleteInformation] = "Informações incompletas",
            [ReasonCode.DuplicateRequest] = "Solicitação duplicada",
            [ReasonCode.ConditionsNotMet] = "Condições de ingresso ou fundação não atendidas",
            [ReasonCode.NewFoundingAfterPrevious] = "Nova fundação após fundação anterior",
            [ReasonCode.TemporaryLeave] = "Afastamento temporário",
            [ReasonCode.AdministrativeReview] = "Revisão administrativa da participação",
            [ReasonCode.RemovalRequested] = "Encerramento da participação solicitado",
            [ReasonCode.AdministrativeClosure] = "Encerramento administrativo",
            [ReasonCode.ReturnAfterLeave] = "Retorno após afastamento",
            [ReasonCode.SuspensionCorrection] = "Correção de suspensão",
            [ReasonCode.DirectorUnavailable] = "Diretor indisponível",
            [ReasonCode.DirectorshipRegularization] = "Regularização da direção",
            [ReasonCode.ClubSupportRequest] = "Suporte solicitado pelo clube",
            [ReasonCode.RegistrationInconsistency] = "Verificação de inconsistência cadastral",
        };

        Assert.Equal(Enum.GetValues<ReasonCode>().Length, expected.Count);
        foreach (var (code, text) in expected)
            Assert.Equal(text, Labels.Of(code));
    }
}
