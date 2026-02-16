namespace CRMS.Application.Common;

public class ApplicationResult<T>
{
    public bool IsSuccess { get; }
    public T? Data { get; }
    public string? Error { get; }
    public List<string> Errors { get; } = [];

    private ApplicationResult(bool isSuccess, T? data, string? error, List<string>? errors = null)
    {
        IsSuccess = isSuccess;
        Data = data;
        Error = error;
        if (errors != null)
            Errors = errors;
    }

    public static ApplicationResult<T> Success(T data) => new(true, data, null);
    public static ApplicationResult<T> Failure(string error) => new(false, default, error);
    public static ApplicationResult<T> Failure(List<string> errors) => new(false, default, errors.FirstOrDefault(), errors);
}

public class ApplicationResult
{
    public bool IsSuccess { get; }
    public string? Error { get; }
    public List<string> Errors { get; } = [];

    private ApplicationResult(bool isSuccess, string? error, List<string>? errors = null)
    {
        IsSuccess = isSuccess;
        Error = error;
        if (errors != null)
            Errors = errors;
    }

    public static ApplicationResult Success() => new(true, null);
    public static ApplicationResult Failure(string error) => new(false, error);
    public static ApplicationResult Failure(List<string> errors) => new(false, errors.FirstOrDefault(), errors);
}
