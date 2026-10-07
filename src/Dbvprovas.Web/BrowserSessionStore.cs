using Dbvprovas.Ui.Services;
using Microsoft.JSInterop;

namespace Dbvprovas.Web;

// No site, a sessão de dev fica no sessionStorage da aba (RNF-TEN-004).
public sealed class BrowserSessionStore(IJSRuntime js) : ISessionStore
{
    private const string Key = "dbvprovas.session";

    public async Task<string?> GetAsync() => await js.InvokeAsync<string?>("sessionStorage.getItem", Key);

    public async Task SetAsync(string token) => await js.InvokeVoidAsync("sessionStorage.setItem", Key, token);

    public async Task ClearAsync() => await js.InvokeVoidAsync("sessionStorage.removeItem", Key);
}
