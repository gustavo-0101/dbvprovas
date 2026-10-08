namespace Dbvprovas.Ui.Services;

// Onde a sessão fica guardada depende do alvo: sessionStorage no site e SecureStorage no
// Android (RNF-TEN-004, PC-11).
public interface ISessionStore
{
    Task<string?> GetAsync();

    Task SetAsync(string token);

    Task ClearAsync();
}

public sealed class SessionState(ISessionStore store)
{
    public string? Token { get; private set; }

    public async Task LoadAsync() => Token ??= await store.GetAsync();

    public async Task SignInAsync(string token)
    {
        Token = token;
        await store.SetAsync(token);
    }

    public async Task SignOutAsync()
    {
        Token = null;
        await store.ClearAsync();
    }
}
