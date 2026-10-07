using Dbvprovas.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDatabase(); // RNF-TEN-001

var app = builder.Build();
app.Run();

public partial class Program;
