namespace Portfolio.Api.Models;

public sealed record ApiResponse<T>(T Data, string Message, object? Meta = null);

public static class ApiResponse
{
    public static ApiResponse<T> Success<T>(T data, string message = "Success") =>
        new(data, message);
}
