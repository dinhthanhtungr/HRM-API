using HRM.Domain.Enums.WorkTaskEnums;

namespace HRM.Application.Features.Work.MyTasks;

internal static class MyTaskRules
{
    public const int MaxTitleLength = 250;
    public const int MaxTextLength = 8000;
    public const int MaxListNameLength = 120;

    public static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static bool IsTerminal(WorkTaskStatus status)
        => status is WorkTaskStatus.Done or WorkTaskStatus.Canceled;
}

internal static class MyTaskMessages
{
    public const string CurrentEmployeeOrCompanyInvalid = "Current employee or company is invalid.";
    public const string TaskTitleInvalid = "Task title is invalid.";
    public const string TaskPriorityInvalid = "Task priority is invalid.";
    public const string TaskStatusOrPriorityInvalid = "Task status or priority is invalid.";
    public const string TaskTextTooLong = "Task description or next action is too long.";
    public const string TaskListNotFound = "Task list was not found.";
    public const string PersonalTaskNotFound = "Personal task was not found.";
    public const string CompletionStatusInvalid = "Completion status must be Done or Canceled.";
    public const string CompletionNoteTooLong = "Completion note is too long.";
    public const string TaskReorderPayloadInvalid = "Task reorder payload is invalid.";
    public const string SomeTaskListsNotFound = "Some task lists were not found.";
    public const string SomePersonalTasksNotFound = "Some personal tasks were not found.";
    public const string TaskListNameInvalid = "Task list name is invalid.";
    public const string TaskListReorderPayloadInvalid = "Task list reorder payload is invalid.";
}
