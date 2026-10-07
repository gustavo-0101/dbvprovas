using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Dbvprovas.Web.E2E;

public sealed class SiteTests(SiteFixture site)
{
    [Fact]
    public async Task CA_TEN_004_Site_shows_only_current_user_club()
    {
        await using var context = await site.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        var members = page.Locator("ul.members li");

        await page.GotoAsync(site.BaseUrl);
        await page.GetByRole(AriaRole.Button, new() { Name = "Aurora — Clube Águias" }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Clube Águias", Level = 1 })).ToBeVisibleAsync();
        await Expect(members).ToHaveTextAsync(new[] { "Aurora", "Caio", "Dalva" });

        // RNF-TEN-004: recarregar a página mantém a sessão guardada no sessionStorage.
        // O Reload só espera o load; o WebAssembly sobe depois, e num runner frio isso passa dos 5 s do padrão.
        await page.ReloadAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Clube Águias", Level = 1 })).ToBeVisibleAsync(new() { Timeout = 30_000 });

        await page.GetByRole(AriaRole.Button, new() { Name = "Trocar de usuário" }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Bento — Clube Corujas" }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Clube Corujas", Level = 1 })).ToBeVisibleAsync();
        await Expect(members).ToHaveTextAsync(new[] { "Bento", "Elisa", "Fábio" });
    }
}
