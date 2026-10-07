using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dbvprovas.Contracts;

namespace Dbvprovas.Ui.Services;

public enum ApiError
{
    Unavailable,
    Unauthorized,
    NotFound,
}

public sealed record ApiResult<T>(T? Value, ApiError? Error)
{
    public bool IsOk => Error is null;

    public static ApiResult<T> Ok(T value) => new(value, null);

    public static ApiResult<T> Fail(ApiError error) => new(default, error);
}

// Cliente da API, igual no app e no site (RNF-TEN-004). As falhas viram estados que a tela
// mostra sem detalhe técnico (RF-TEN-004).
public sealed class ApiClient(HttpClient http, SessionState session)
{
    public Task<ApiResult<List<DevAccountResponse>>> GetDevAccountsAsync() =>
        SendAsync<List<DevAccountResponse>>(HttpMethod.Get, ApiRoutes.DevAccounts);

    public Task<ApiResult<DevSessionResponse>> CreateDevSessionAsync(Guid accountId) =>
        SendAsync<DevSessionResponse>(HttpMethod.Post, ApiRoutes.DevSessions, new DevSessionRequest(accountId));

    public Task<ApiResult<MeResponse>> GetMeAsync() =>
        SendAsync<MeResponse>(HttpMethod.Get, ApiRoutes.Me);

    public Task<ApiResult<ClubResponse>> GetClubAsync(Guid clubId) =>
        SendAsync<ClubResponse>(HttpMethod.Get, ApiRoutes.Club(clubId));

    public Task<ApiResult<List<MemberResponse>>> GetMembersAsync(Guid clubId) =>
        SendAsync<List<MemberResponse>>(HttpMethod.Get, ApiRoutes.Members(clubId));

    private async Task<ApiResult<T>> SendAsync<T>(HttpMethod method, string path, object? body = null)
    {
        try
        {
            using var request = new HttpRequestMessage(method, path);
            if (session.Token is { } token)
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (body is not null)
                request.Content = JsonContent.Create(body);

            using var response = await http.SendAsync(request);
            if (response.IsSuccessStatusCode)
                return ApiResult<T>.Ok((await response.Content.ReadFromJsonAsync<T>())!);

            return ApiResult<T>.Fail(response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => ApiError.Unauthorized,
                HttpStatusCode.NotFound => ApiError.NotFound,
                _ => ApiError.Unavailable,
            });
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            return ApiResult<T>.Fail(ApiError.Unavailable);
        }
    }
}
