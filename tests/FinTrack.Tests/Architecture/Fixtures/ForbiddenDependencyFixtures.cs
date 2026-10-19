namespace FinTrack.Tests.Architecture.Fixtures;

// Tipos propositalmente violadores, usados só para provar que as regras detectam violações.
internal static class DomainLikeLeakingToInfrastructure
{
    public static Type Leak() => typeof(Infrastructure.AssemblyMarker);
}

internal static class ApiLikeTouchingDomainAndInfrastructure
{
    public static Type[] Leak() => [typeof(Domain.AssemblyMarker), typeof(Infrastructure.AssemblyMarker)];
}
