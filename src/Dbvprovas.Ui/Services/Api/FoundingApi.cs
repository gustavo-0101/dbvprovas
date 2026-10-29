using Dbvprovas.Contracts;

namespace Dbvprovas.Ui.Services.Api;

// Chamadas da fundação, dos dados do clube e do convite.
public sealed class FoundingApi(ApiClient api)
{
    internal ApiClient Api { get; } = api;

    public Task<ApiResult<FoundingRequestResponse>> CreateAsync(CreateFoundingRequest body) =>
        Api.SendAsync<FoundingRequestResponse>(HttpMethod.Post, ApiRoutes.FoundingRequests, body);

    public Task<ApiResult<List<FoundingRequestResponse>>> ListMineAsync() =>
        Api.SendAsync<List<FoundingRequestResponse>>(HttpMethod.Get, ApiRoutes.MyFoundingRequests);

    public Task<ApiResult> CancelAsync(Guid requestId) => Api.SendAsync(HttpMethod.Post, ApiRoutes.CancelMyFoundingRequest(requestId));

    public Task<ApiResult<List<AdminFoundingRequestResponse>>> ListPendingAsync() =>
        Api.SendAsync<List<AdminFoundingRequestResponse>>(HttpMethod.Get, ApiRoutes.AdminFoundingRequests);

    public Task<ApiResult<FoundingApprovedResponse>> ApproveAsync(Guid requestId, ApproveFoundingRequest body) =>
        Api.SendAsync<FoundingApprovedResponse>(HttpMethod.Post, ApiRoutes.ApproveFoundingRequest(requestId), body);

    public Task<ApiResult> RejectAsync(Guid requestId, RejectFoundingRequest body) =>
        Api.SendAsync(HttpMethod.Post, ApiRoutes.RejectFoundingRequest(requestId), body);

    public Task<ApiResult<ClubSettingsResponse>> GetSettingsAsync(Guid clubId) =>
        Api.SendAsync<ClubSettingsResponse>(HttpMethod.Get, ApiRoutes.ClubSettings(clubId));

    public Task<ApiResult<ClubSettingsResponse>> SaveSettingsAsync(Guid clubId, ClubSettingsRequest body) =>
        Api.SendAsync<ClubSettingsResponse>(HttpMethod.Put, ApiRoutes.ClubSettings(clubId), body);

    public Task<ApiResult<ClubSettingsResponse>> RotateInviteAsync(Guid clubId) =>
        Api.SendAsync<ClubSettingsResponse>(HttpMethod.Post, ApiRoutes.ClubInviteCode(clubId));
}
