namespace MITANZ360Pro.Web.Common;

public class ServiceResult
{
    public bool IsSuccess { get; set; }

    public string? ErrorMessage { get; set; }

    public static ServiceResult Success()
        => new() { IsSuccess = true };

    public static ServiceResult Failure(string message)
        => new()
        {
            IsSuccess = false,
            ErrorMessage = message
        };
}

public sealed class ServiceResult<T> : ServiceResult
{
    public T? Data { get; set; }

    public static ServiceResult<T> Success(T data)
        => new()
        {
            IsSuccess = true,
            Data = data
        };

    public new static ServiceResult<T> Failure(string message)
        => new()
        {
            IsSuccess = false,
            ErrorMessage = message
        };
}
