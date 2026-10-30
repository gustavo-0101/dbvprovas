using System.Net;
using System.Net.Http.Json;
using Dbvprovas.Api.Tests.Infrastructure;
using Dbvprovas.Contracts;

namespace Dbvprovas.Api.Tests.Organization;

public sealed class OrgPersonalDataLeakTests(PostgresFixture db)
{
    // CA-AUD-004 (extensão R1): os fluxos de fundação e organização com valores fictícios únicos não deixam nenhum deles em log, trace ou erro.
    [Fact]
    public async Task CA_AUD_004_Org_flows_never_log_personal_values()
    {
        using var probe = new LeakProbe();
        var requester = await TestData.CreatePersonAsync(db, probe.Name, email: probe.Email, subject: probe.GoogleSubject, birthDate: probe.BirthDate);
        var admin = await TestData.CreateAdminAsync(db);
        await using var api = new ApiFactory(db, "Production") { UseTestAuthentication = true };
        using var client = api.CreateClientAs(requester);
        using var adminClient = api.CreateClientAs(admin);

        using var sent = await client.PostAsJsonAsync(ApiRoutes.FoundingRequests,
            new CreateFoundingRequest(probe.ClubName, probe.City, null, false, ConsentTerms.AdultOwnParticipationVersion, true));
        Assert.True(sent.IsSuccessStatusCode);
        var request = await sent.Content.ReadFromJsonAsync<FoundingRequestResponse>();
        var pending = await adminClient.GetStringAsync($"{ApiRoutes.AdminFoundingRequests}?limit=100");
        Assert.Contains(probe.Name, pending);
        using var approved = await adminClient.PostAsJsonAsync(ApiRoutes.ApproveFoundingRequest(request!.Id), new ApproveFoundingRequest(false, null));
        Assert.True(approved.IsSuccessStatusCode);
        var clubId = (await approved.Content.ReadFromJsonAsync<FoundingApprovedResponse>())!.ClubId;
        using var saved = await client.PutAsJsonAsync(ApiRoutes.ClubSettings(clubId), new ClubSettingsRequest(probe.ClubName, probe.City, null));
        Assert.True(saved.IsSuccessStatusCode);
        var settings = await client.GetFromJsonAsync<ClubSettingsResponse>(ApiRoutes.ClubSettings(clubId));
        probe.Track(settings!.InviteCode);
        foreach (var bad in new[]
        {
            await client.PostAsJsonAsync(ApiRoutes.Units(clubId), new UnitRequest(probe.Name + "\u0007", Sex.Female, 10, 12)),
            await client.PostAsJsonAsync(ApiRoutes.RoleAssignments(clubId), new AssignRoleRequest(Guid.NewGuid(), RoleCode.Chaplain, null)),
            await client.PostAsJsonAsync(ApiRoutes.SuspendMember(clubId, Guid.NewGuid()), new MemberStatusRequest(null)),
        })
        {
            Assert.False(bad.IsSuccessStatusCode);
            probe.ErrorBodies.Add(await bad.Content.ReadAsStringAsync());
        }

        probe.AssertAbsent(api);
    }
}
