using System.Xml.Linq;

namespace FinTrack.Tests.Architecture;

internal static class ProjectReferenceRules
{
    public static readonly IReadOnlyDictionary<string, string[]> Allowed = new Dictionary<string, string[]>
    {
        ["FinTrack.Domain"] = [],
        ["FinTrack.Application"] = ["FinTrack.Domain"],
        ["FinTrack.Infrastructure"] = ["FinTrack.Application", "FinTrack.Domain"],
        ["FinTrack.Api"] = ["FinTrack.Application", "FinTrack.Infrastructure"],
        ["FinTrack.Tests"] = ["FinTrack.Api", "FinTrack.Application", "FinTrack.Domain", "FinTrack.Infrastructure"],
    };

    public static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "FinTrack.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("FinTrack.sln não encontrado.");
    }

    public static Dictionary<string, string[]> ReadActual(string root)
    {
        var paths = new Dictionary<string, string>
        {
            ["FinTrack.Domain"] = "src/FinTrack.Domain/FinTrack.Domain.csproj",
            ["FinTrack.Application"] = "src/FinTrack.Application/FinTrack.Application.csproj",
            ["FinTrack.Infrastructure"] = "src/FinTrack.Infrastructure/FinTrack.Infrastructure.csproj",
            ["FinTrack.Api"] = "src/FinTrack.Api/FinTrack.Api.csproj",
            ["FinTrack.Tests"] = "tests/FinTrack.Tests/FinTrack.Tests.csproj",
        };

        return paths.ToDictionary(
            p => p.Key,
            p => XDocument.Load(Path.Combine(root, p.Value))
                .Descendants("ProjectReference")
                .Select(e => Path.GetFileNameWithoutExtension(((string?)e.Attribute("Include") ?? string.Empty).Replace('\\', '/').Split('/')[^1]))
                .ToArray());
    }

    public static IReadOnlyList<string> Validate(IReadOnlyDictionary<string, string[]> actual)
    {
        var violations = new List<string>();
        foreach (var (project, allowed) in Allowed)
        {
            var refs = actual.TryGetValue(project, out var r) ? r : [];
            violations.AddRange(refs.Except(allowed).Select(x => $"{project} -> {x} proibida"));
            violations.AddRange(allowed.Except(refs).Select(x => $"{project} -> {x} ausente"));
        }

        return violations;
    }
}
