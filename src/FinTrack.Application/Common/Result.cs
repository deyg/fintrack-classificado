namespace FinTrack.Application.Common;

public sealed class Result<T>
{
    private Result(T? value, IReadOnlyDictionary<string, string[]>? validationErrors, bool isNotFound)
    {
        Value = value;
        ValidationErrors = validationErrors;
        IsNotFound = isNotFound;
    }

    public T? Value { get; }

    public IReadOnlyDictionary<string, string[]>? ValidationErrors { get; }

    public bool IsNotFound { get; }

    public bool IsSuccess => ValidationErrors is null && !IsNotFound;

    public static Result<T> Success(T value) => new(value, null, false);

    public static Result<T> Invalid(IReadOnlyDictionary<string, string[]> errors) => new(default, errors, false);

    public static Result<T> NotFound() => new(default, null, true);
}
