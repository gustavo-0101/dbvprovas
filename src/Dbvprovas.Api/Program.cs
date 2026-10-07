using Dbvprovas.Api.Infrastructure;
using Dbvprovas.Api.Modules.Audit;
using Dbvprovas.Api.Modules.Identity;
using Dbvprovas.Api.Modules.Tenancy;
using Dbvprovas.Api.Site;

var builder = WebApplication.CreateBuilder(args);

builder.AddObservability(); // D-115, D-116
builder.Services.AddDatabase(); // RNF-TEN-001
builder.AddSessions(); // D-120, RNF-TEN-002
builder.Services.AddHealth(); // RF-AUD-001
builder.Services.AddProblemDetails(); // RF-AUD-002
builder.Services.AddOpenApi(); // D-100
builder.Services.AddRazorComponents().AddInteractiveWebAssemblyComponents(); // D-111, D-112
// Remove o fallback {**path:file} que o MapStaticAssets registra em dev e que ficaria anônimo (CA-TEN-006).
builder.Configuration["DisableStaticAssetNotFoundRuntimeFallback"] = "true";

var app = builder.Build();

app.UseExceptionHandler(new ExceptionHandlerOptions
{
    StatusCodeSelector = exception => exception is BadHttpRequestException badRequest
        ? badRequest.StatusCode
        : StatusCodes.Status500InternalServerError,
});
app.UseRouting();
app.UseUnmatchedRouteNotFound();
app.UseAuthentication();
app.UsePersonContext();
app.UseAuthorization();
app.UseAntiforgery(); // D-112

app.MapHealth();
app.MapMe();
app.MapClubs();

// O site é anônimo de forma explícita (CA-TEN-006). Só os endpoints de resource-collection do
// MapRazorComponents ignoram as convenções de cada chamada (dotnet/aspnetcore#65327), por isso o
// AllowAnonymous vai no grupo; voltar ao AllowAnonymous por chamada quando o framework os cobrir.
// Caminho desconhecido, inclusive /api/..., segue sendo o 404 da API, nunca a página (D-112).
var site = app.MapGroup("");
site.MapStaticAssets();
site.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(Dbvprovas.Ui.Routes).Assembly);
site.AllowAnonymous();

if (app.Environment.IsDevelopment())
{
    app.MapDevLogin();
    app.MapOpenApi().AllowAnonymous();
    await DevDatabase.InitializeAsync(app.Services); // RF-TEN-003
}

app.Run();

public partial class Program;
