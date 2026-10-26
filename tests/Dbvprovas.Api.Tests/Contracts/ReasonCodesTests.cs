using Dbvprovas.Contracts;

namespace Dbvprovas.Api.Tests.Contracts;

public sealed class ReasonCodesTests
{
    // D-160: as opções de cada operação são exatamente as da tabela confirmada, e todo código pertence a uma operação.
    [Fact]
    public void CA_AUD_010_Reason_categories_match_the_confirmed_table()
    {
        Assert.Equal(new[] { ReasonCode.IncompleteInformation, ReasonCode.DuplicateRequest, ReasonCode.ConditionsNotMet },
            ReasonCodes.AllowedFor(ReasonOperation.RejectRequest));
        Assert.Equal(new[] { ReasonCode.NewFoundingAfterPrevious }, ReasonCodes.AllowedFor(ReasonOperation.FoundingException));
        Assert.Equal(new[] { ReasonCode.TemporaryLeave, ReasonCode.AdministrativeReview }, ReasonCodes.AllowedFor(ReasonOperation.Suspend));
        Assert.Equal(new[] { ReasonCode.RemovalRequested, ReasonCode.AdministrativeClosure }, ReasonCodes.AllowedFor(ReasonOperation.Remove));
        Assert.Equal(new[] { ReasonCode.ReturnAfterLeave, ReasonCode.SuspensionCorrection }, ReasonCodes.AllowedFor(ReasonOperation.Reactivate));
        Assert.Equal(new[] { ReasonCode.DirectorUnavailable, ReasonCode.DirectorshipRegularization }, ReasonCodes.AllowedFor(ReasonOperation.ReplaceDirector));
        Assert.Equal(new[] { ReasonCode.ClubSupportRequest, ReasonCode.RegistrationInconsistency }, ReasonCodes.AllowedFor(ReasonOperation.AdminAccessRequest));
        Assert.Equal(Enum.GetValues<ReasonCode>().Order(), Enum.GetValues<ReasonOperation>().SelectMany(ReasonCodes.AllowedFor).Order());
    }

    // D-160: motivo de outra operação ou ausente não vale.
    [Theory]
    [InlineData(ReasonOperation.Suspend, ReasonCode.ReturnAfterLeave)]
    [InlineData(ReasonOperation.Remove, ReasonCode.TemporaryLeave)]
    [InlineData(ReasonOperation.RejectRequest, ReasonCode.NewFoundingAfterPrevious)]
    public void CA_AUD_010_Reason_from_another_operation_is_not_allowed(ReasonOperation operation, ReasonCode code)
    {
        Assert.False(ReasonCodes.IsAllowed(operation, code));
        Assert.False(ReasonCodes.IsAllowed(operation, null));
    }
}
