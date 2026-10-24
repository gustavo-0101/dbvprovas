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
}
