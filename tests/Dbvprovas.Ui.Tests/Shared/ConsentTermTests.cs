using Dbvprovas.Contracts;
using Dbvprovas.Ui.Components;

namespace Dbvprovas.Ui.Tests.Shared;

public sealed class ConsentTermTests
{
    // RN-PRV-002 (S-073): o termo mostra o clube e a versão, e o aceite sobe para a tela.
    [Fact]
    public void Consent_term_shows_club_and_version_and_reports_acceptance()
    {
        using var ui = new UiHarness();
        var accepted = false;

        var term = ui.Context.Render<ConsentTerm>(p => p
            .Add(c => c.ClubName, "Clube Águias")
            .Add(c => c.AcceptedChanged, (bool value) => accepted = value));
        term.Find("input[type=checkbox]").Change(true);

        Assert.Contains("Clube Águias", term.Markup);
        Assert.Contains(ConsentTerms.AdultOwnParticipationVersion, term.Markup);
        Assert.True(accepted);
    }

    // RN-PRV-002: desmarcar devolve falso à tela.
    [Fact]
    public void Consent_term_reports_withdrawal_when_unchecked()
    {
        using var ui = new UiHarness();
        var reported = new List<bool>();

        var term = ui.Context.Render<ConsentTerm>(p => p
            .Add(c => c.ClubName, "Clube Águias")
            .Add(c => c.Accepted, true)
            .Add(c => c.AcceptedChanged, (bool value) => reported.Add(value)));
        term.Find("input[type=checkbox]").Change(false);

        Assert.Equal([false], reported);
    }

    // RN-PRV-002: o checkbox reflete o aceite que a tela passa.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Consent_term_checkbox_reflects_the_accepted_parameter(bool accepted)
    {
        using var ui = new UiHarness();

        var term = ui.Context.Render<ConsentTerm>(p => p
            .Add(c => c.ClubName, "Clube Águias")
            .Add(c => c.Accepted, accepted));

        Assert.Equal(accepted, term.Find("input[type=checkbox]").HasAttribute("checked"));
    }
}
