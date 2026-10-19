using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace FinTrack.Tests.Http;

public sealed class ApiBootstrapHttpTests : IClassFixture<ApiBootstrapHttpTests.Factory>
{
    private readonly Factory _factory;

    public ApiBootstrapHttpTests(Factory factory) => _factory = factory;

    [Fact]
    public async Task Health_returns_200_without_persistence()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Unknown_route_returns_problem_details_404()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/nao-existe");

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "/api/v1/nao-existe");
    }

    [Fact]
    public async Task Unhandled_exception_returns_problem_details_500_without_leaking_internals()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/__test/boom");

        var body = await AssertProblemAsync(response, HttpStatusCode.InternalServerError, "/__test/boom");
        Assert.DoesNotContain("segredo-interno", body, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidOperationException", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Malformed_request_returns_problem_details_400()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/__test/bad");

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "/__test/bad");
    }

    [Fact]
    public async Task OpenApi_document_is_exposed_only_in_Development()
    {
        var development = _factory.WithWebHostBuilder(b => b.UseEnvironment("Development")).CreateClient();
        var production = _factory.WithWebHostBuilder(b => b.UseEnvironment("Production")).CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await development.GetAsync("/openapi/v1.json")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await production.GetAsync("/openapi/v1.json")).StatusCode);
    }

    private static async Task<string> AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string instance)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;
        Assert.Equal((int)status, root.GetProperty("status").GetInt32());
        Assert.Equal($"https://httpstatuses.com/{(int)status}", root.GetProperty("type").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("title").GetString()));
        Assert.Equal(instance, root.GetProperty("instance").GetString());
        return body;
    }

    public sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) =>
            builder.ConfigureServices(services => services.AddSingleton<IStartupFilter, FaultyEndpointsFilter>());
    }

    private sealed class FaultyEndpointsFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
            app =>
            {
                next(app);
                app.Use(async (context, nextMiddleware) =>
                {
                    switch (context.Request.Path.Value)
                    {
                        case "/__test/boom":
                            throw new InvalidOperationException("segredo-interno");
                        case "/__test/bad":
                            throw new BadHttpRequestException("corpo inválido");
                        default:
                            await nextMiddleware(context);
                            break;
                    }
                });
            };
    }
}
