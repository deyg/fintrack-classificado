using FinTrack.Api.Composition;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddFinTrackApi();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthChecks("/health");

app.Run();

public partial class Program;
