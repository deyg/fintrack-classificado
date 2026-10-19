namespace FinTrack.Application.Common;

public sealed class Result<T>
{
    private Result(T? value, IReadOnlyDictionary<string, string[]>? validationErrors)
    {
        Value = value;
        ValidationErrors = validationErrors;
    }

    public T? Value { get; }

    public IReadOnlyDictionary<string, string[]>? ValidationErrors { get; }

    public bool IsSuccess => ValidationErrors is null;

    public static Result<T> Success(T value) => new(value, null);

    public static Result<T> Invalid(IReadOnlyDictionary<string, string[]> errors) => new(default, errors);
}
