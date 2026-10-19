using System.Reflection;
using NetArchTest.Rules;

namespace FinTrack.Tests.Architecture;

public sealed class LayerDependencyArchitectureTests
{
    private const string Api = "FinTrack.Api";
    private const string Application = "FinTrack.Application";
    private const string Domain = "FinTrack.Domain";
    private const string Infrastructure = "FinTrack.Infrastructure";
    private const string Fixtures = "FinTrack.Tests.Architecture.Fixtures";

    private static readonly string[] Frameworks =
    [
        "Microsoft.EntityFrameworkCore",
        "Microsoft.AspNetCore",
        "Microsoft.Data.SqlClient",
        "FluentValidation",
    ];

    private static readonly Assembly ApiAssembly = typeof(FinTrack.Api.AssemblyMarker).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(FinTrack.Application.AssemblyMarker).Assembly;
    private static readonly Assembly DomainAssembly = typeof(FinTrack.Domain.AssemblyMarker).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(FinTrack.Infrastructure.AssemblyMarker).Assembly;

    [Fact]
    public void Project_references_match_the_allowed_matrix()
    {
        var actual = ProjectReferenceRules.ReadActual(ProjectReferenceRules.FindRepositoryRoot());

        Assert.Empty(ProjectReferenceRules.Validate(actual));
    }

    [Fact]
    public void Project_reference_rule_detects_simulated_violations()
    {
        var simulated = new Dictionary<string, string[]>(ProjectReferenceRules.ReadActual(ProjectReferenceRules.FindRepositoryRoot()))
        {
            ["FinTrack.Api"] = ["FinTrack.Application", "FinTrack.Infrastructure", "FinTrack.Domain"],
            ["FinTrack.Application"] = ["FinTrack.Domain", "FinTrack.Infrastructure"],
            ["FinTrack.Infrastructure"] = ["FinTrack.Application", "FinTrack.Domain", "FinTrack.Api"],
        };

        var violations = ProjectReferenceRules.Validate(simulated);

        Assert.Contains("FinTrack.Api -> FinTrack.Domain proibida", violations);
        Assert.Contains("FinTrack.Application -> FinTrack.Infrastructure proibida", violations);
        Assert.Contains("FinTrack.Infrastructure -> FinTrack.Api proibida", violations);
    }

    [Fact]
    public void Domain_has_no_dependency_on_other_layers_or_frameworks()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot().HaveDependencyOnAny([Api, Application, Infrastructure, .. Frameworks])
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Application_has_no_dependency_on_Api_Infrastructure_or_web_and_persistence_frameworks()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot().HaveDependencyOnAny([Api, Infrastructure, "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "Microsoft.Data.SqlClient"])
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Infrastructure_has_no_dependency_on_Api()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .ShouldNot().HaveDependencyOn(Api)
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Api_does_not_depend_on_Domain_or_persistence_frameworks()
    {
        var result = Types.InAssembly(ApiAssembly)
            .ShouldNot().HaveDependencyOnAny([Domain, "Microsoft.EntityFrameworkCore", "Microsoft.Data.SqlClient"])
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Api_uses_Infrastructure_only_in_the_Composition_Root()
    {
        var result = Types.InAssembly(ApiAssembly)
            .That().DoNotResideInNamespace("FinTrack.Api.Composition").And().DoNotHaveName("Program")
            .ShouldNot().HaveDependencyOn(Infrastructure)
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Dependency_rules_detect_simulated_violations()
    {
        var domainLike = Types.InAssembly(typeof(LayerDependencyArchitectureTests).Assembly)
            .That().HaveName("DomainLikeLeakingToInfrastructure")
            .ShouldNot().HaveDependencyOn(Infrastructure)
            .GetResult();

        var apiLike = Types.InAssembly(typeof(LayerDependencyArchitectureTests).Assembly)
            .That().HaveName("ApiLikeTouchingDomainAndInfrastructure")
            .ShouldNot().HaveDependencyOnAny([Domain, Infrastructure])
            .GetResult();

        Assert.False(domainLike.IsSuccessful);
        Assert.False(apiLike.IsSuccessful);
        Assert.Contains(domainLike.FailingTypeNames, n => n.StartsWith(Fixtures, StringComparison.Ordinal));
    }

    private static string Describe(TestResult result) =>
        result.IsSuccessful ? string.Empty : "Violações: " + string.Join(", ", result.FailingTypeNames ?? []);
}
