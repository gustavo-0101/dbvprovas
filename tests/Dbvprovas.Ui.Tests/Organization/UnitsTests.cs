using System.Net;
using Dbvprovas.Contracts;
using Dbvprovas.Ui.Pages.Organization;

namespace Dbvprovas.Ui.Tests.Organization;

public sealed class UnitsTests
{
    private static readonly Guid UnitId = Guid.Parse("00000000-0000-7000-8000-0000000000c1");

    private static void ServeUnits(UiHarness ui) =>
        ui.Api.Respond(HttpMethod.Get, ApiRoutes.Units(UiHarness.ClubId), HttpStatusCode.OK, new[]
        {
            new UnitResponse(UnitId, "Unidade Sol", Sex.Female, 10, 12, UnitStatus.Active),
        });

    private static void FillNewUnit(IRenderedComponent<Units> page)
    {
        page.Find("input.unit-name").Change("Unidade Lua");
        page.Find("select.unit-sex").Change(nameof(Sex.Male));
        page.Find("input.unit-min").Change("11");
        page.Find("input.unit-max").Change("13");
    }

    // CA-ORG-007 na tela: cria a unidade com a faixa e o sexo escolhidos.
    [Fact]
    public void CA_ORG_007_Units_page_creates_unit()
    {
        using var ui = new UiHarness();
        OrgUi.ServeMe(ui, false, RoleCode.Director);
        ServeUnits(ui);
        ui.Api.Respond(HttpMethod.Post, ApiRoutes.Units(UiHarness.ClubId), HttpStatusCode.OK,
            new UnitResponse(Guid.NewGuid(), "Unidade Lua", Sex.Male, 11, 13, UnitStatus.Active));

        var page = ui.Context.Render<Units>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("li.unit")));
        Assert.Single(page.FindAll("h1"));
        Assert.Equal("60", page.Find("input.unit-name").GetAttribute("maxlength"));
        Assert.Contains("Unidade Sol", page.Markup);

        FillNewUnit(page);
        page.Find("form.unit-form").Submit();

        page.WaitForAssertion(() =>
        {
            var body = Assert.Single(ui.Api.Bodies, b => b.Key == $"POST /{ApiRoutes.Units(UiHarness.ClubId)}").Body;
            Assert.Contains("\"name\":\"Unidade Lua\"", body);
            Assert.Contains("\"sex\":\"Male\"", body);
            Assert.Contains("\"minAge\":11", body);
            Assert.Contains("\"maxAge\":13", body);
        });
    }

    [Fact]
    public void Units_page_explains_invalid_range()
    {
        using var ui = new UiHarness();
        OrgUi.ServeMe(ui, false, RoleCode.Director);
        ServeUnits(ui);
        ui.Api.RespondText(HttpMethod.Post, ApiRoutes.Units(UiHarness.ClubId), HttpStatusCode.BadRequest,
            $"{{\"code\":\"{ProblemCodes.UnitInvalidRange}\"}}", "application/problem+json");

        var page = ui.Context.Render<Units>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("li.unit")));
        FillNewUnit(page);
        page.Find("form.unit-form").Submit();

        page.WaitForAssertion(() => Assert.Contains("A faixa vai de 10 a 15 anos, com a mínima menor ou igual à máxima.", page.Markup));
    }

    [Fact]
    public void Units_page_explains_sex_conflict()
    {
        using var ui = new UiHarness();
        OrgUi.ServeMe(ui, false, RoleCode.Secretary);
        ServeUnits(ui);
        ui.Api.RespondText(HttpMethod.Put, ApiRoutes.Unit(UiHarness.ClubId, UnitId), HttpStatusCode.Conflict,
            $"{{\"code\":\"{ProblemCodes.UnitSexConflict}\"}}", "application/problem+json");

        var page = ui.Context.Render<Units>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("li.unit")));
        page.Find("button.edit").Click();
        Assert.Equal("Unidade Sol", page.Find("input.unit-name").GetAttribute("value"));
        page.Find("select.unit-sex").Change(nameof(Sex.Male));
        page.Find("form.unit-form").Submit();

        page.WaitForAssertion(() => Assert.Contains("Há conselheiro de outro sexo nesta unidade. Retire a atribuição antes de mudar o sexo.", page.Markup));
        Assert.Contains(ui.Api.Requests, r => r.StartsWith($"PUT /{ApiRoutes.Unit(UiHarness.ClubId, UnitId)}"));
    }
}
