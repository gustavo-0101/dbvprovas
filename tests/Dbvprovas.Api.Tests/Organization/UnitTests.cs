using System.Net;
using System.Net.Http.Json;
using Dbvprovas.Api.Tests.Infrastructure;
using Dbvprovas.Contracts;

namespace Dbvprovas.Api.Tests.Organization;

public sealed class UnitTests(PostgresFixture db)
{
    private ApiFactory Api() => new(db, "Production") { UseTestAuthentication = true };

    // a: diretora e secretário cadastram e editam; nenhum líder é alocado automaticamente (RN-ORG-017).
    [Fact]
    public async Task CA_ORG_007_Director_and_secretary_create_and_edit_units()
    {
        var a = await TestData.CreateClubAAsync(db);
        await using var api = Api();
        using var aurora = api.CreateClientAs(a.Aurora);
        using var caio = api.CreateClientAs(a.Caio);

        var created = await (await aurora.PostAsJsonAsync(ApiRoutes.Units(a.ClubId), new UnitRequest(" Unidade W ", Sex.Male, 12, 14))).Content.ReadFromJsonAsync<UnitResponse>();
        using var edited = await caio.PutAsJsonAsync(ApiRoutes.Unit(a.ClubId, created!.Id), new UnitRequest("Unidade W2", Sex.Male, 11, 15, UnitStatus.Inactive));
        var list = await caio.GetFromJsonAsync<List<UnitResponse>>(ApiRoutes.Units(a.ClubId));

        Assert.Equal("Unidade W", created.Name);
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        Assert.Contains(list!, u => u.Id == created.Id && u.Name == "Unidade W2" && u.Status == UnitStatus.Inactive);
        Assert.Equal(0, await OwnerSql.ScalarAsync<long>(db.OwnerConnectionString,
            "SELECT count(*) FROM role_assignments WHERE unit_id = @u", ("u", created.Id)));
    }

    // b: outros cargos; c: criar ou editar em B; d: sem cargo, faixa invertida ou fora de 10 a 15.
    [Fact]
    public async Task CA_ORG_007_Other_roles_other_club_and_invalid_units_are_denied()
    {
        var a = await TestData.CreateClubAAsync(db);
        var b = await TestData.CreateClubBAsync(db);
        await using var api = Api();
        using var aurora = api.CreateClientAs(a.Aurora);
        var valid = new UnitRequest("Unidade", Sex.Female, 10, 12);

        await ProblemAssert.HasCodeAsync(await api.CreateClientAs(a.Lina).PostAsJsonAsync(ApiRoutes.Units(a.ClubId), valid), HttpStatusCode.Forbidden, ProblemCodes.PermissionDenied);
        await ProblemAssert.HasCodeAsync(await api.CreateClientAs(a.Iara).PostAsJsonAsync(ApiRoutes.Units(a.ClubId), valid), HttpStatusCode.Forbidden, ProblemCodes.PermissionDenied);
        Assert.Equal(HttpStatusCode.NotFound, (await aurora.PostAsJsonAsync(ApiRoutes.Units(b.ClubId), valid)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await aurora.PutAsJsonAsync(ApiRoutes.Unit(a.ClubId, b.UnitZ), valid)).StatusCode);
        await ProblemAssert.HasCodeAsync(await aurora.PostAsJsonAsync(ApiRoutes.Units(a.ClubId), valid with { MinAge = 14, MaxAge = 12 }),
            HttpStatusCode.BadRequest, ProblemCodes.UnitInvalidRange);
        await ProblemAssert.HasCodeAsync(await aurora.PostAsJsonAsync(ApiRoutes.Units(a.ClubId), valid with { MinAge = 9 }),
            HttpStatusCode.BadRequest, ProblemCodes.UnitInvalidRange);
        await ProblemAssert.HasCodeAsync(await aurora.PostAsJsonAsync(ApiRoutes.Units(a.ClubId), valid with { MaxAge = 16 }),
            HttpStatusCode.BadRequest, ProblemCodes.UnitInvalidRange);
    }

    // d, D-164: mudar o sexo da unidade com conselheira incompatível é recusado até corrigir a atribuição.
    [Fact]
    public async Task CA_ORG_007_Changing_sex_with_incompatible_counselor_is_refused()
    {
        var a = await TestData.CreateClubAAsync(db);
        await using var api = Api();

        using var refused = await api.CreateClientAs(a.Aurora).PutAsJsonAsync(ApiRoutes.Unit(a.ClubId, a.UnitX), new UnitRequest("Unidade X", Sex.Male, 10, 15));

        await ProblemAssert.HasCodeAsync(refused, HttpStatusCode.Conflict, ProblemCodes.UnitSexConflict);
        Assert.Equal(1, await OwnerSql.ScalarAsync<long>(db.OwnerConnectionString,
            "SELECT count(*) FROM role_assignments WHERE unit_id = @u AND ended_at IS NULL", ("u", a.UnitX)));
    }

    // RF-ORG-001: nome de unidade vazio, com mais de 60 caracteres ou com caractere de controle.
    [Fact]
    public async Task Unit_rejects_blank_huge_or_control_name()
    {
        var a = await TestData.CreateClubAAsync(db);
        await using var api = Api();
        using var aurora = api.CreateClientAs(a.Aurora);

        foreach (var name in new[] { " ", new string('u', 61), "Uni\tdade" })
            await ProblemAssert.HasCodeAsync(await aurora.PostAsJsonAsync(ApiRoutes.Units(a.ClubId), new UnitRequest(name, Sex.Female, 10, 12)),
                HttpStatusCode.BadRequest, ProblemCodes.ValidationText);
    }
}
