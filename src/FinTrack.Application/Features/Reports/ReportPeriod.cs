namespace FinTrack.Application.Features.Reports;

public sealed record ReportPeriodQuery(DateOnly? From, DateOnly? To);

public sealed record ReportPeriod(DateOnly From, DateOnly To);

// DEC-REP-008: from/to obrigatórios (ausência = 400); período invertido é erro de valor (422), sob a chave "period".
public static class ReportPeriodValidator
{
    public const string InvertedPeriodField = "period";

    public static bool TryCreate(ReportPeriodQuery query, out ReportPeriod? period, out Dictionary<string, string[]> errors)
    {
        errors = [];
        period = null;

        if (query.From is null)
        {
            errors["from"] = ["A data inicial é obrigatória."];
        }

        if (query.To is null)
        {
            errors["to"] = ["A data final é obrigatória."];
        }

        if (errors.Count == 0 && query.To < query.From)
        {
            errors[InvertedPeriodField] = ["A data final deve ser maior ou igual à data inicial."];
        }

        if (errors.Count > 0)
        {
            return false;
        }

        period = new ReportPeriod(query.From!.Value, query.To!.Value);
        return true;
    }

    public static bool IsValueError(IReadOnlyDictionary<string, string[]> errors) => errors.ContainsKey(InvertedPeriodField);
}
