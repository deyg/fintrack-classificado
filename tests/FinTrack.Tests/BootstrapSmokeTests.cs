namespace FinTrack.Tests;

public sealed class BootstrapSmokeTests
{
    [Fact]
    public void Solution_assemblies_are_loadable()
    {
        string?[] names =
        [
            typeof(Api.AssemblyMarker).Assembly.GetName().Name,
            typeof(Application.AssemblyMarker).Assembly.GetName().Name,
            typeof(Domain.AssemblyMarker).Assembly.GetName().Name,
            typeof(Infrastructure.AssemblyMarker).Assembly.GetName().Name,
        ];

        Assert.Equal(
            new string?[] { "FinTrack.Api", "FinTrack.Application", "FinTrack.Domain", "FinTrack.Infrastructure" },
            names);
    }

    [Fact]
    public void Runtime_targets_net10()
    {
        Assert.Equal(10, Environment.Version.Major);
    }
}
