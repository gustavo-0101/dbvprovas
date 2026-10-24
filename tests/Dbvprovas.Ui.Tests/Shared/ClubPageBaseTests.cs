using System.Net;
using Dbvprovas.Contracts;
using Dbvprovas.Ui.Components;
using Dbvprovas.Ui.Services;

namespace Dbvprovas.Ui.Tests.Shared;

// Base das telas do R1 (D-104): carga de "quem sou eu", cargos que a tela usa para esconder ações (PC-09) e mensagens de erro.
public sealed class ClubPageBaseTests
{
    private const string ModuleText = "Texto do módulo.";

    [Fact]
    public void Load_without_a_valid_session_clears_it_and_returns_to_login_with_replace()
    {
        using var ui = new UiHarness();
        ui.Api.Respond(HttpMethod.Get, ApiRoutes.Me, HttpStatusCode.Unauthorized);
        ui.Navigation.NavigateTo("club");

        var page = Open(ui);

        Assert.False(page.Instance.Loaded);
        Assert.Null(ui.Store.Token);
        Assert.Equal("http://localhost/", ui.Navigation.Uri);
        Assert.True(ui.LastNavigation.Options.ReplaceHistoryEntry);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.NotFound)]
    public void Load_with_the_server_failing_is_unavailable_and_keeps_the_session(HttpStatusCode status)
    {
        using var ui = new UiHarness();
        ui.Api.Respond(HttpMethod.Get, ApiRoutes.Me, status);

        var page = Open(ui);

        Assert.False(page.Instance.Loaded);
        Assert.Equal(PageState.Unavailable, page.Instance.CurrentState);
        Assert.NotNull(ui.Store.Token);
        Assert.Null(page.Instance.CurrentMe);
    }

    [Fact]
    public void Load_with_the_server_down_is_unavailable()
    {
        using var ui = new UiHarness();
        ui.Api.Offline = true;

        var page = Open(ui);

        Assert.False(page.Instance.Loaded);
        Assert.Equal(PageState.Unavailable, page.Instance.CurrentState);
        Assert.NotNull(ui.Store.Token);
    }

    [Fact]
    public void Load_exposes_the_first_club_and_its_roles()
    {
        using var ui = new UiHarness();
        ui.Api.Respond(HttpMethod.Get, ApiRoutes.Me, HttpStatusCode.OK, new MeResponse("Aurora",
            [
                new ClubSummary(UiHarness.ClubId, "Clube Águias", Guid.NewGuid(), [RoleCode.Director, RoleCode.Instructor]),
                new ClubSummary(Guid.NewGuid(), "Clube Falcões", Guid.NewGuid(), [RoleCode.Treasurer]),
            ], true, true, false));

        var page = Open(ui);

        Assert.True(page.Instance.Loaded);
        Assert.Equal("Clube Águias", page.Instance.ActiveClub!.Name);
        Assert.True(page.Instance.HasRole(RoleCode.Instructor));
        Assert.False(page.Instance.HasRole(RoleCode.Treasurer));
    }

    [Fact]
    public void Without_a_club_no_role_or_board_gate_applies()
    {
        using var ui = new UiHarness();
        ui.Api.Respond(HttpMethod.Get, ApiRoutes.Me, HttpStatusCode.OK, new MeResponse("Aurora", [], true, true, false));

        var page = Open(ui);

        Assert.True(page.Instance.Loaded);
        Assert.Null(page.Instance.ActiveClub);
        Assert.False(page.Instance.HasRole(RoleCode.Director));
        Assert.False(page.Instance.DirectorOrSecretary);
        Assert.False(page.Instance.Board);
    }

    // PC-09: a tela só esconde; quem decide é a API. Quem não é adulto nunca recebe os botões da diretoria,
    // mesmo com o cargo no cadastro.
    [Theory]
    [InlineData(true, RoleCode.Director, true, true)]
    [InlineData(true, RoleCode.Secretary, true, true)]
    [InlineData(true, RoleCode.AssociateDirector, false, true)]
    [InlineData(true, RoleCode.Treasurer, false, true)]
    [InlineData(true, RoleCode.Chaplain, false, true)]
    [InlineData(true, RoleCode.Pastor, false, true)]
    [InlineData(true, RoleCode.Counselor, false, false)]
    [InlineData(true, RoleCode.AssociateCounselor, false, false)]
    [InlineData(true, RoleCode.Instructor, false, false)]
    [InlineData(false, RoleCode.Director, false, false)]
    [InlineData(false, RoleCode.Secretary, false, false)]
    [InlineData(false, RoleCode.Treasurer, false, false)]
    public void Board_gates_need_an_adult_with_the_role(bool isAdult, RoleCode role, bool directorOrSecretary, bool board)
    {
        using var ui = new UiHarness();
        ui.Api.Respond(HttpMethod.Get, ApiRoutes.Me, HttpStatusCode.OK,
            new MeResponse("Aurora", [new ClubSummary(UiHarness.ClubId, "Clube Águias", Guid.NewGuid(), [role])], true, isAdult, false));

        var page = Open(ui);

        Assert.True(page.Instance.HasRole(role));
        Assert.Equal(directorOrSecretary, page.Instance.DirectorOrSecretary);
        Assert.Equal(board, page.Instance.Board);
    }

    [Fact]
    public async Task Succeeded_without_error_clears_the_message()
    {
        using var ui = new UiHarness();
        ui.ServeClubAguias();
        var page = Open(ui);
        await page.Instance.RunAsync(ApiError.Conflict, ProblemCodes.StateConflict);
        Assert.NotNull(page.Instance.CurrentMessage);

        var done = await page.Instance.RunAsync(null, null);

        Assert.True(done);
        Assert.Null(page.Instance.CurrentMessage);
    }

    [Fact]
    public async Task Succeeded_with_401_clears_the_session_and_returns_to_login_with_replace()
    {
        using var ui = new UiHarness();
        ui.ServeClubAguias();
        var page = Open(ui);
        ui.Navigation.NavigateTo("club");

        var done = await page.Instance.RunAsync(ApiError.Unauthorized, null);

        Assert.False(done);
        Assert.Null(ui.Store.Token);
        Assert.Equal("http://localhost/", ui.Navigation.Uri);
        Assert.True(ui.LastNavigation.Options.ReplaceHistoryEntry);
        Assert.Null(page.Instance.CurrentMessage);
    }

    // D-125: o texto do módulo, quando existe para o código, vem antes do texto comum.
    [Fact]
    public async Task Succeeded_prefers_the_module_text_for_its_own_code()
    {
        using var ui = new UiHarness();
        ui.ServeClubAguias();
        var page = Open(ui);

        var done = await page.Instance.RunAsync(ApiError.Conflict, ProblemCodes.StateConflict, code => code == ProblemCodes.StateConflict ? ModuleText : null);

        Assert.False(done);
        Assert.Equal(ModuleText, page.Instance.CurrentMessage);
    }

    [Fact]
    public async Task Succeeded_falls_back_to_the_common_text_when_the_module_has_none()
    {
        using var ui = new UiHarness();
        ui.ServeClubAguias();
        var page = Open(ui);

        await page.Instance.RunAsync(ApiError.Conflict, ProblemCodes.StateConflict, _ => null);
        Assert.Equal("Isso já foi decidido ou mudou enquanto você olhava. Atualize a página.", page.Instance.CurrentMessage);

        await page.Instance.RunAsync(ApiError.Forbidden, ProblemCodes.PermissionDenied);
        Assert.Equal("Você não tem permissão para isso.", page.Instance.CurrentMessage);

        await page.Instance.RunAsync(ApiError.Invalid, "codigo.desconhecido");
        Assert.Equal("Não foi possível concluir. Tente de novo.", page.Instance.CurrentMessage);
    }

    [Fact]
    public void Describe_maps_availability_errors_without_technical_detail()
    {
        Assert.Equal("Não foi possível falar com o servidor.", ProbePage.Text(ApiError.Unavailable, "state.conflict"));
        Assert.Equal("Não encontrado.", ProbePage.Text(ApiError.NotFound, "state.conflict"));
        Assert.Equal(ModuleText, ProbePage.Text(ApiError.Forbidden, "x", _ => ModuleText));
        Assert.Equal("Você não tem permissão para isso.", ProbePage.Text(ApiError.Forbidden, ProblemCodes.PermissionDenied));
    }

    private static IRenderedComponent<ProbePage> Open(UiHarness ui)
    {
        var page = ui.Context.Render<ProbePage>();
        page.WaitForState(() => page.Instance.Done);
        return page;
    }

    // Tela de teste: expõe os membros protegidos da base.
    private sealed class ProbePage : ClubPageBase
    {
        public bool Done { get; private set; }

        public bool Loaded { get; private set; }

        public MeResponse? CurrentMe => Me;

        public ClubSummary? ActiveClub => Club;

        public PageState CurrentState => State;

        public string? CurrentMessage => Message;

        public bool DirectorOrSecretary => IsDirectorOrSecretary;

        public bool Board => IsBoard;

        public static string Text(ApiError? error, string? code, Func<string?, string?>? module = null) => Describe(error, code, module);

        public bool HasRole(RoleCode role) => Has(role);

        public Task<bool> RunAsync(ApiError? error, string? code, Func<string?, string?>? module = null) => SucceededAsync(error, code, module);

        protected override async Task OnInitializedAsync()
        {
            Loaded = await LoadMeAsync();
            Done = true;
        }
    }
}
