using System.Net;
using System.Net.Http.Json;
using System.Text;
using FinTrack.Application.Features.Reports;
using FinTrack.Application.Features.Transactions;
using FinTrack.Tests.Architecture;
using FinTrack.Tests.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace FinTrack.Tests.Http;

// REL-002: erros consistentes, sem vazamento de exceção/SQL e sem dados financeiros em logs.
public sealed class HardeningContractTests : IAsyncLifetime
{
    private const string LeakyMessage = "SELECT * FROM Transactions WHERE Password='segredo-sql'";
    private const string SensitiveDescription = "descricao-sensivel-123";

    private readonly SqlServerDatabaseFixture _database = new();

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        await using var context = _database.CreateContext();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _database.DisposeAsync();

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly List<string> _entries = [];

        public IReadOnlyList<string> Entries
        {
            get
            {
                lock (_entries)
                {
                    return _entries.ToList();
                }
            }
        }

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

        public void Dispose()
        {
        }

        private void Add(string entry)
        {
            lock (_entries)
            {
                _entries.Add(entry);
            }
        }

        private sealed class CapturingLogger(CapturingLoggerProvider owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) =>
                owner.Add(formatter(state, exception) + exception?.ToString());
        }
    }

    private sealed class ThrowingReports : IReadReportRepository
    {
        public Task<SummaryTotals> GetSummaryAsync(ReportPeriod period, CancellationToken cancellationToken) =>
            throw new InvalidOperationException(LeakyMessage);

        public Task<IReadOnlyList<CategoryTotalRow>> GetByCategoryAsync(ReportPeriod period, CancellationToken cancellationToken) =>
            throw new InvalidOperationException(LeakyMessage);

        public Task<IReadOnlyList<AccountTotalRow>> GetByAccountAsync(ReportPeriod period, CancellationToken cancellationToken) =>
            throw new InvalidOperationException(LeakyMessage);

        public Task<IReadOnlyList<ClassifierTotalRow>> GetByClassifierAsync(ReportPeriod period, CancellationToken cancellationToken) =>
            throw new InvalidOperationException(LeakyMessage);
    }

    private WebApplicationFactory<Program> CreateFactory(
        string environment,
        bool throwingReports = false,
        CapturingLoggerProvider? logs = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.UseSetting("ConnectionStrings:FinTrack", _database.ConnectionString);
            builder.ConfigureServices(services =>
            {
                if (throwingReports)
                {
                    services.RemoveAll<IReadReportRepository>();
                    services.AddScoped<IReadReportRepository, ThrowingReports>();
                }

                if (logs is not null)
                {
                    services.AddLogging(l => l.AddProvider(logs));
                }
            });
        });

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        using var json = System.Text.Json.JsonDocument.Parse(body);
        Assert.Equal((int)expected, json.RootElement.GetProperty("status").GetInt32());
        Assert.Equal($"https://httpstatuses.com/{(int)expected}", json.RootElement.GetProperty("type").GetString());
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task Excecao_nao_tratada_retorna_500_sem_vazar_excecao_nem_sql(string environment)
    {
        await using var factory = CreateFactory(environment, throwingReports: true);

        var response = await factory.CreateClient().GetAsync("/api/v1/reports/summary?from=2026-10-01&to=2026-10-31");

        await AssertProblemAsync(response, HttpStatusCode.InternalServerError);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("SELECT", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("segredo-sql", body);
        Assert.DoesNotContain("InvalidOperationException", body);
        Assert.DoesNotContain("   at ", body);
        Assert.DoesNotContain("FinTrack.Infrastructure", body);
    }

    [Fact]
    public async Task Logs_de_erro_nao_contem_dados_financeiros_da_requisicao()
    {
        var logs = new CapturingLoggerProvider();
        await using var factory = CreateFactory("Production", throwingReports: true, logs);

        await factory.CreateClient().GetAsync("/api/v1/reports/summary?from=2026-10-01&to=2026-10-31");
        await factory.CreateClient().PostAsJsonAsync(
            "/api/v1/transactions",
            new { accountId = 1, amount = 98765.43m, realizedAt = "2026-10-01", description = SensitiveDescription });

        var all = string.Join('\n', logs.Entries);
        Assert.Contains("Erro não tratado", all);
        Assert.DoesNotContain(SensitiveDescription, all);
        Assert.DoesNotContain("98765", all);
    }

    [Fact]
    public async Task Json_malformado_retorna_400_sem_detalhe_do_parser()
    {
        await using var factory = CreateFactory("Production");

        var response = await factory.CreateClient().PostAsync(
            "/api/v1/categories", new StringContent("{ nome: ", Encoding.UTF8, "application/json"));

        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("JsonException", body);
        Assert.DoesNotContain("LineNumber", body);
    }

    [Fact]
    public async Task Respostas_400_404_422_usam_problem_details_consistente()
    {
        await using var factory = CreateFactory("Production");
        var client = factory.CreateClient();

        await AssertProblemAsync(await client.GetAsync("/api/v1/transactions/abc"), HttpStatusCode.BadRequest);
        await AssertProblemAsync(await client.GetAsync("/api/v1/transactions/999999"), HttpStatusCode.NotFound);
        await AssertProblemAsync(
            await client.PostAsJsonAsync("/api/v1/categories", new { name = "", nature = "EXPENSE" }),
            HttpStatusCode.UnprocessableEntity);
        await AssertProblemAsync(await client.GetAsync("/api/v1/rota-inexistente"), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Conflito_409_usa_problem_details()
    {
        await using var factory = CreateFactory("Production");
        var client = factory.CreateClient();
        var category = await client.PostAsJsonAsync("/api/v1/categories", new { name = "Moradia", nature = "EXPENSE" });
        Assert.Equal(HttpStatusCode.Created, category.StatusCode);
        using var categoryJson = System.Text.Json.JsonDocument.Parse(await category.Content.ReadAsStringAsync());
        var categoryId = categoryJson.RootElement.GetProperty("id").GetInt32();
        var account = await client.PostAsJsonAsync(
            "/api/v1/accounts", new { categoryId, name = "Aluguel", classifier = "OF" });
        Assert.Equal(HttpStatusCode.Created, account.StatusCode);

        var blocked = await client.DeleteAsync($"/api/v1/categories/{categoryId}");

        await AssertProblemAsync(blocked, HttpStatusCode.Conflict);
    }

    [Fact]
    public void Arquivos_versionados_nao_contem_segredos()
    {
        var root = ProjectReferenceRules.FindRepositoryRoot();
        var tracked = new[] { "src/FinTrack.Api/appsettings.json", "src/FinTrack.Api/appsettings.Development.json", ".env.example", "compose.yaml" }
            .Select(path => Path.Combine(root, path))
            .Where(File.Exists);

        foreach (var file in tracked)
        {
            // Linhas de comentário com exemplos não são segredos.
            var text = string.Join('\n', File.ReadAllLines(file).Where(l => !l.TrimStart().StartsWith('#')));
            Assert.DoesNotMatch(@"(?i)password[ \t]*=[ \t]*[^;\s""$]+", text);
            Assert.DoesNotMatch(@"(?im)^[ \t]*MSSQL_SA_PASSWORD[ \t]*=[ \t]*\S+", text);
        }

        Assert.Contains(".env", File.ReadAllLines(Path.Combine(root, ".gitignore")).Select(l => l.Trim()));
    }
}
