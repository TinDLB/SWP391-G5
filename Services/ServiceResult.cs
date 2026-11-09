namespace SWP391_G5.Services;

public class ServiceResult
{
    public bool Success { get; }
    public string Message { get; }
    private ServiceResult(bool success, string message) { Success = success; Message = message; }

    public static ServiceResult Ok(string message = "") => new(true, message);
    public static ServiceResult Fail(string message) => new(false, message);
}