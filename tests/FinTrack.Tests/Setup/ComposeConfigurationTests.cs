using System.Text.Json;
using FinTrack.Tests.Architecture;
using Microsoft.Extensions.Configuration;

namespace FinTrack.Tests.Setup;

public sealed class ComposeConfigurationTests
{
    private static readonly string Root = ProjectReferenceRules.FindRepositoryRoot();

    private static string Read(string relative) => File.ReadAllText(Path.Combine(Root, relative));

    [Fact]
    public void Compose_defines_sqlserver_with_healthcheck_and_password_from_environment()
    {
        var compose = Read("compose.yaml");

        Assert.Contains("mcr.microsoft.com/mssql/server:", compose, StringComparison.Ordinal);
        Assert.Contains("healthcheck:", compose, StringComparison.Ordinal);
        Assert.Contains("MSSQL_SA_PASSWORD: ${MSSQL_SA_PASSWORD:?", compose, StringComparison.Ordinal);
    }

    [Fact]
    public void Env_example_has_no_secret_value()
    {
        var line = Read(".env.example").Split('\n')
            .Single(l => l.StartsWith("MSSQL_SA_PASSWORD=", StringComparison.Ordinal));

        Assert.Equal("MSSQL_SA_PASSWORD=", line.Trim());
    }

    [Fact]
    public void Gitignore_protects_env_files_but_keeps_the_example()
    {
        var lines = Read(".gitignore").Split('\n').Select(l => l.Trim()).ToArray();

        Assert.Contains(".env", lines);
        Assert.Contains("!.env.example", lines);
    }

    [Fact]
    public void Committed_appsettings_contain_no_connection_secret()
    {
        foreach (var file in new[] { "appsettings.json", "appsettings.Development.json" })
        {
            using var json = JsonDocument.Parse(Read($"src/FinTrack.Api/{file}"));
            var text = json.RootElement.GetRawText();

            Assert.DoesNotContain("Password", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Pwd=", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Connection_string_comes_from_environment_variable_over_appsettings()
    {
        var path = Path.Combine(Root, "src/FinTrack.Api/appsettings.json");
        const string key = "ConnectionStrings__FinTrack";
        Environment.SetEnvironmentVariable(key, "Server=probe");
        try
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile(path, optional: false)
                .AddEnvironmentVariables()
                .Build();

            Assert.Equal("Server=probe", configuration.GetConnectionString("FinTrack"));
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    [Fact]
    public void Connection_string_is_empty_in_committed_configuration()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(Root, "src/FinTrack.Api/appsettings.json"), optional: false)
            .Build();

        Assert.True(string.IsNullOrEmpty(configuration.GetConnectionString("FinTrack")));
    }
}
