namespace FinTrack.Application.Common;

public sealed class Result<T>
{
    private Result(T? value, IReadOnlyDictionary<string, string[]>? validationErrors, bool isNotFound, string? conflict)
    {
        Value = value;
        ValidationErrors = validationErrors;
        IsNotFound = isNotFound;
        ConflictMessage = conflict;
    }

    public T? Value { get; }

    public IReadOnlyDictionary<string, string[]>? ValidationErrors { get; }

    public bool IsNotFound { get; }

    public string? ConflictMessage { get; }

    public bool IsConflict => ConflictMessage is not null;

    public bool IsSuccess => ValidationErrors is null && !IsNotFound && !IsConflict;

    public static Result<T> Success(T value) => new(value, null, false, null);

    public static Result<T> Invalid(IReadOnlyDictionary<string, string[]> errors) => new(default, errors, false, null);

    public static Result<T> NotFound() => new(default, null, true, null);

    public static Result<T> Conflict(string message) => new(default, null, false, message);
}
