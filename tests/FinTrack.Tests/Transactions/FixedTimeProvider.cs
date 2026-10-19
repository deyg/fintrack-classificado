namespace FinTrack.Tests.Transactions;

// Relógio fixo em UTC para tornar determinísticos os testes de data de realização.
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}
