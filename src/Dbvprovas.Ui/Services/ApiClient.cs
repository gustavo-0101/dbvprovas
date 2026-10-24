using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dbvprovas.Contracts;

namespace Dbvprovas.Ui.Services;

public enum ApiError
{
    Unavailable,
    Unauthorized,
    NotFound,
    Forbidden,
    Conflict,
    Invalid,
}

public sealed record ApiResult<T>(T? Value, ApiError? Error, string? ProblemCode = null)
{
    public bool IsOk => Error is null;

    public static ApiResult<T> Ok(T value) => new(value, null);

    public static ApiResult<T> Fail(ApiError error, string? problemCode = null) => new(default, error, problemCode);
}

public sealed record ApiResult(ApiError? Error, string? ProblemCode = null)
{
    public bool IsOk => Error is null;

    // O argumento nomeado evita o construtor de cópia do registro.
    public static ApiResult Ok() => new(Error: null);

    public static ApiResult Fail(ApiError error, string? problemCode = null) => new(error, problemCode);
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

    public async Task<ApiResult<T>> SendAsync<T>(HttpMethod method, string path, object? body = null)
    {
        var (response, error, code) = await ExchangeAsync(method, path, body);
        using (response)
        {
            if (error is not null)
                return ApiResult<T>.Fail(error.Value, code);
            try
            {
                // Um 2xx com corpo que não serve (página de proxy, corpo nulo) é o mesmo que sem servidor.
                var value = await response!.Content.ReadFromJsonAsync<T>();
                return value is null ? ApiResult<T>.Fail(ApiError.Unavailable) : ApiResult<T>.Ok(value);
            }
            catch (Exception e) when (e is JsonException or NotSupportedException or HttpRequestException or WebException or TaskCanceledException)
            {
                return ApiResult<T>.Fail(ApiError.Unavailable);
            }
        }
    }

    public async Task<ApiResult> SendAsync(HttpMethod method, string path, object? body = null)
    {
        var (response, error, code) = await ExchangeAsync(method, path, body);
        response?.Dispose();
        return error is null ? ApiResult.Ok() : ApiResult.Fail(error.Value, code);
    }

    private async Task<(HttpResponseMessage? Response, ApiError? Error, string? Code)> ExchangeAsync(HttpMethod method, string path, object? body)
    {
        try
        {
            using var request = new HttpRequestMessage(method, path);
            if (session.Token is { } token)
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (body is not null)
                request.Content = JsonContent.Create(body);

            var response = await http.SendAsync(request);
            if (response.IsSuccessStatusCode)
                return (response, null, null);

            var error = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => ApiError.Unauthorized,
                HttpStatusCode.NotFound => ApiError.NotFound,
                HttpStatusCode.Forbidden => ApiError.Forbidden,
                HttpStatusCode.Conflict => ApiError.Conflict,
                HttpStatusCode.BadRequest => ApiError.Invalid,
                _ => ApiError.Unavailable,
            };
            var code = error is ApiError.Forbidden or ApiError.Conflict or ApiError.Invalid ? await ReadProblemCodeAsync(response) : null;
            response.Dispose();
            return (null, error, code);
        }
        // WebException: no Android, socket fechado e prazo estourado sobem assim, não como HttpRequestException.
        catch (Exception e) when (e is HttpRequestException or WebException or TaskCanceledException)
        {
            return (null, ApiError.Unavailable, null);
        }
    }

    // O código estável do ProblemDetails (extensão "code", D-125); corpo inesperado vira código ausente.
    private static async Task<string?> ReadProblemCodeAsync(HttpResponseMessage response)
    {
        try
        {
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty(ProblemCodes.Extension, out var code)
                && code.ValueKind == JsonValueKind.String ? code.GetString() : null;
        }
        catch (Exception e) when (e is JsonException or HttpRequestException or WebException or TaskCanceledException)
        {
            return null;
        }
    }
}
