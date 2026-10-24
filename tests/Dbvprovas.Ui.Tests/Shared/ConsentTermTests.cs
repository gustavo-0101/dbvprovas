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
}
