using Dbvprovas.Contracts;
using Dbvprovas.Ui.Services;
using Dbvprovas.Ui.Text;
using Microsoft.AspNetCore.Components;

namespace Dbvprovas.Ui.Components;

public enum PageState
{
    Loading,
    Ready,
    Unavailable,
    Forbidden,
    NoClub,
}

// Base das telas do R1, iguais no app e no site (D-104): carrega "quem sou eu" e o clube ativo. A tela só esconde ações;
// quem decide é a API (PC-09). Sem sessão, volta ao login com replace, como o "Meu clube" do R0.
public abstract class ClubPageBase : ComponentBase
{
    [Inject]
    protected ApiClient Api { get; set; } = default!;

    [Inject]
    protected SessionState Session { get; set; } = default!;

    [Inject]
    protected NavigationManager Navigation { get; set; } = default!;

    protected MeResponse? Me { get; private set; }

    protected ClubSummary? Club => Me is { Clubs.Count: > 0 } me ? me.Clubs[0] : null;

    protected PageState State { get; set; } = PageState.Loading;

    protected string? Message { get; set; }

    protected bool Has(RoleCode role) => Club?.Roles.Contains(role) == true;

    protected bool IsDirectorOrSecretary => Me?.IsAdult == true && (Has(RoleCode.Director) || Has(RoleCode.Secretary));

    protected bool IsBoard => Me?.IsAdult == true && Club?.Roles.Any(r => r is RoleCode.Director or RoleCode.AssociateDirector
        or RoleCode.Secretary or RoleCode.Treasurer or RoleCode.Chaplain or RoleCode.Pastor) == true;

    // Devolve falso quando a tela deve parar: sem sessão (vai ao login) ou servidor fora (estado indisponível).
    protected async Task<bool> LoadMeAsync()
    {
        await Session.LoadAsync();
        var me = await Api.GetMeAsync();
        if (me.Error == ApiError.Unauthorized)
        {
            await Session.SignOutAsync();
            Navigation.NavigateTo(UiRoutes.Login, replace: true);
            return false;
        }

        if (!me.IsOk)
        {
            State = PageState.Unavailable;
            return false;
        }

        Me = me.Value;
        return true;
    }

    // Depois de uma ação: 401 volta ao login; o resto vira mensagem. Devolve verdadeiro se deu certo.
    protected async Task<bool> SucceededAsync(ApiError? error, string? code, Func<string?, string?>? module = null)
    {
        if (error is null)
        {
            Message = null;
            return true;
        }

        if (error == ApiError.Unauthorized)
        {
            await Session.SignOutAsync();
            Navigation.NavigateTo(UiRoutes.Login, replace: true);
            return false;
        }

        Message = Describe(error, code, module);
        return false;
    }

    protected static string Describe(ApiError? error, string? code, Func<string?, string?>? module = null) => error switch
    {
        ApiError.Unavailable => "Não foi possível falar com o servidor.",
        ApiError.NotFound => "Não encontrado.",
        _ => module?.Invoke(code) ?? Labels.Problem(code),
    };
}
