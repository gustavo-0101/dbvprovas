using Dbvprovas.Api.Infrastructure;
using Dbvprovas.Api.Modules.Audit;
using Dbvprovas.Api.Modules.Identity;
using Dbvprovas.Api.Modules.Tenancy;

var builder = WebApplication.CreateBuilder(args);

builder.AddObservability(); // D-115, D-116
builder.Services.AddDatabase(); // RNF-TEN-001
builder.AddSessions(); // D-120, RNF-TEN-002
builder.Services.AddHealth(); // RF-AUD-001
builder.Services.AddProblemDetails(); // RF-AUD-002

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

app.MapHealth();
app.MapMe();
app.MapClubs();

if (app.Environment.IsDevelopment())
{
    app.MapDevLogin();
    await DevDatabase.InitializeAsync(app.Services); // RF-TEN-003
}

app.Run();

public partial class Program;
