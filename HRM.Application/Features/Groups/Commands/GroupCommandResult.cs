namespace HRM.Application.Features.Groups.Commands;

public enum GroupCommandError
{
    None,
    Validation,
    Forbidden,
    NotFound,
    Conflict
}

public sealed class GroupCommandResult<T>
{
    private GroupCommandResult(bool success, T? data, string? message, GroupCommandError error)
    {
        Success = success;
        Data = data;
        Message = message;
        Error = error;
    }

    public bool Success { get; }
    public T? Data { get; }
    public string? Message { get; }
    public GroupCommandError Error { get; }

    public static GroupCommandResult<T> Ok(T data)
        => new(true, data, null, GroupCommandError.None);

    public static GroupCommandResult<T> Fail(GroupCommandError error, string message)
        => new(false, default, message, error);
}
