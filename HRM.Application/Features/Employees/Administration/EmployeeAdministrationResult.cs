namespace HRM.Application.Features.Employees.Administration;

public enum EmployeeAdministrationError
{
    None,
    Validation,
    Forbidden,
    NotFound,
    Conflict
}

public sealed class EmployeeAdministrationResult<T>
{
    private EmployeeAdministrationResult(
        bool success,
        T? data,
        string? message,
        EmployeeAdministrationError error)
    {
        Success = success;
        Data = data;
        Message = message;
        Error = error;
    }

    public bool Success { get; }
    public T? Data { get; }
    public string? Message { get; }
    public EmployeeAdministrationError Error { get; }

    public static EmployeeAdministrationResult<T> Ok(T data)
        => new(true, data, null, EmployeeAdministrationError.None);

    public static EmployeeAdministrationResult<T> Fail(
        EmployeeAdministrationError error,
        string message)
        => new(false, default, message, error);
}
