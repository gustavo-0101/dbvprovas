using Dbvprovas.Api.Infrastructure;
using Dbvprovas.Api.Modules.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDatabase(); // RNF-TEN-001
builder.AddSessions(); // D-120, RNF-TEN-002

var app = builder.Build();

app.UseRouting();
app.UseUnmatchedRouteNotFound();
app.UseAuthentication();
app.UsePersonContext();
app.UseAuthorization();

app.MapMe();

if (app.Environment.IsDevelopment())
{
    app.MapDevLogin();
    await DevDatabase.InitializeAsync(app.Services); // RF-TEN-003
}

app.Run();

public partial class Program;
