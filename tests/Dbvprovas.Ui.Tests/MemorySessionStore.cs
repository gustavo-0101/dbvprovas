using Dbvprovas.Ui.Services;

namespace Dbvprovas.Ui.Tests;

internal sealed class MemorySessionStore : ISessionStore
{
    public string? Token { get; set; }

    public Task<string?> GetAsync() => Task.FromResult(Token);

    public Task SetAsync(string token)
    {
        Token = token;
        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        Token = null;
        return Task.CompletedTask;
    }
}
