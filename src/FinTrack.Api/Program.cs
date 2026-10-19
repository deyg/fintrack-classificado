using FinTrack.Api.Composition;
using FinTrack.Api.Endpoints.Accounts;
using FinTrack.Api.Endpoints.Categories;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddFinTrackApi(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthChecks("/health");
app.MapGroup("/api/v1").MapCategoryEndpoints().MapAccountEndpoints();

app.Run();

public partial class Program;
