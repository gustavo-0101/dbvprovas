using Android.Content;
using Dbvprovas.Ui.Services;

namespace Dbvprovas.App;

// No Android, a sessão de dev fica no armazenamento seguro do sistema (RNF-TEN-004). Ele pode falhar
// (chave perdida, valor corrompido, tipos Java): ler ou gravar falhando vira sessão ausente, nunca erro
// na tela. Só trocar de usuário falha alto, para não deixar um token guardado em silêncio.
internal sealed class SecureSessionStore : ISessionStore
{
    private const string Key = "dbvprovas.session";

    // Arquivo de preferências por baixo do SecureStorage do MAUI 10.0.110: <ApplicationId>.microsoft.maui.essentials.preferences.
    // Esse nome é um alias interno, montado em SecureStorage.shared.cs (Alias) e Preferences.shared.cs (GetPrivatePreferencesSharedName):
    // https://github.com/dotnet/maui/blob/10.0.110/src/Essentials/src/SecureStorage/SecureStorage.shared.cs#L207
    // O Dbvprovas.App.csproj dá erro de build quando a versão do Microsoft.Maui.Controls muda, para reconferir o nome (RNF-TEN-004).
    // O MAUI abre um EncryptedSharedPreferences novo a cada chamada, então apagar o arquivo recupera um keyset corrompido sem reiniciar o app.
    // Com a chave-mestra do Keystore quebrada a limpeza não cura, mas a falha continua fechada: a sessão fica só em memória.
    private static string PreferencesFile => $"{AppInfo.Current.PackageName}.microsoft.maui.essentials.preferences";

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
        catch (Exception) // chaves irrecuperáveis: o Remove também falha, então apaga o arquivo por baixo; se isso falhar, sobe (RNF-TEN-004)
        {
            WipePreferencesFile();
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
            try
            {
                WipePreferencesFile();
            }
            catch (Exception) // idem
            {
            }
        }
    }

    // Limpeza síncrona e independente do EncryptedSharedPreferences, que é o que falha com chaves irrecuperáveis.
    private static void WipePreferencesFile()
    {
        var context = Android.App.Application.Context;
        using var editor = context.GetSharedPreferences(PreferencesFile, FileCreationMode.Private)?.Edit();
        if (editor?.Clear()?.Commit() != true)
            throw new InvalidOperationException("Could not clear the secure storage file.");
    }
}
