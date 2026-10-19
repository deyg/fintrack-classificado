using FinTrack.Tests.Architecture;

namespace FinTrack.Tests.Setup;

public sealed class CiWorkflowTests
{
    private static readonly string Workflow = File.ReadAllText(
        Path.Combine(ProjectReferenceRules.FindRepositoryRoot(), ".github", "workflows", "ci.yml"));

    [Fact]
    public void Workflow_runs_on_Linux_and_Windows_with_the_pinned_SDK()
    {
        Assert.Contains("ubuntu-latest", Workflow, StringComparison.Ordinal);
        Assert.Contains("windows-latest", Workflow, StringComparison.Ordinal);
        Assert.Contains("global-json-file: global.json", Workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void Workflow_runs_the_existing_gates()
    {
        foreach (var gate in new[] { "verify-manifest.ps1", "check-architecture.ps1", "dotnet restore", "dotnet build", "dotnet test", "up -d --wait sqlserver", "probe-sqlserver.ps1" })
        {
            Assert.Contains(gate, Workflow, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Workflow_does_not_mask_failures_or_use_secrets()
    {
        Assert.DoesNotContain("continue-on-error", Workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("|| true", Workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("secrets.", Workflow, StringComparison.Ordinal);
        Assert.Contains("contents: read", Workflow, StringComparison.Ordinal);
    }
}
