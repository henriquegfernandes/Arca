namespace Arca.Application.Common;

public sealed class Result
{
    private Result(bool isSuccess, string? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string? Error { get; }

    public static Result Success() => new(true, null);
    public static Result Failure(string error) => new(false, error);
}

public sealed class Result<T>
{
    private Result(T? value, bool isSuccess, string? error)
    {
        Value = value;
        IsSuccess = isSuccess;
        Error = error;
    }

    public T? Value { get; }
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string? Error { get; }

    public static Result<T> Success(T value) => new(value, true, null);
    public static Result<T> Failure(string error) => new(default, false, error);
}
