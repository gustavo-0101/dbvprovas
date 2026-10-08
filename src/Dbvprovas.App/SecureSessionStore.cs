using Dbvprovas.Ui.Services;

namespace Dbvprovas.App;

// No Android, a sessão de dev fica no armazenamento seguro do sistema (RNF-TEN-004). Ele pode falhar
// (chave perdida, valor corrompido, tipos Java): ler ou gravar falhando vira sessão ausente, nunca erro
// na tela. Só trocar de usuário falha alto, para não deixar um token guardado em silêncio.
internal sealed class SecureSessionStore : ISessionStore
{
    private const string Key = "dbvprovas.session";

    public async Task<string?> GetAsync()
    {
        try
        {
            return await SecureStorage.Default.GetAsync(Key);
        }
        catch (Exception) // qualquer falha do armazenamento seguro é sessão ausente (RNF-TEN-004)
        {
            TryRemove();
            return null;
        }
    }

    public async Task SetAsync(string token)
    {
        try
        {
            await SecureStorage.Default.SetAsync(Key, token);
        }
        catch (Exception) // sem guardar, a sessão vale só em memória e no próximo início está ausente (RNF-TEN-004)
        {
            TryRemove();
        }
    }

    public Task ClearAsync()
    {
        try
        {
            SecureStorage.Default.Remove(Key);
        }
        catch (Exception) // segunda tentativa; se RemoveAll também falhar, a falha sobe (RNF-TEN-004)
        {
            SecureStorage.Default.RemoveAll();
        }

        return Task.CompletedTask;
    }

    // Melhor esforço: o valor ilegível não deve ficar guardado, mas a falha ao apagar não vai para a tela.
    private static void TryRemove()
    {
        try
        {
            SecureStorage.Default.Remove(Key);
        }
        catch (Exception) // nada em log (RNF-PRV-001); o chamador já segue sem sessão
        {
        }
    }
}
