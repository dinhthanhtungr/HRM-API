using System.Text.Json.Serialization;
using HRM.Application.Commons.Pagination;
using HRM.Domain.Enums.WorkTaskEnums;

namespace HRM.Application.Features.Work.MyTasks.Dtos;

/// <summary>
/// Request tạo list/header task cá nhân của employee hiện tại.
/// </summary>
public sealed class CreateMyTaskListRequest
{
    public string Name { get; init; } = string.Empty;
    public int? SortOrder { get; init; }
    public bool IsDefault { get; init; }
}

/// <summary>
/// Request patch list/header task cá nhân.
/// </summary>
public sealed class UpdateMyTaskListRequest
{
    public string? Name { get; init; }
    public int? SortOrder { get; init; }
    public bool? IsDefault { get; init; }
    public bool? IsActive { get; init; }
}

public sealed class ReorderMyTaskListRequest
{
    public IReadOnlyList<ReorderMyTaskListItemDto> Items { get; init; } = [];
}

public sealed class ReorderMyTaskListItemDto
{
    public Guid ListId { get; init; }
    public int SortOrder { get; init; }
}

public sealed class MyTaskListDto
{
    public Guid ListId { get; init; }
    public string Name { get; init; } = string.Empty;
    public int SortOrder { get; init; }
    public bool IsDefault { get; init; }
    public bool IsActive { get; init; }
}

/// <summary>
/// Bộ lọc cho màn Việc của tôi.
/// </summary>
public sealed class MyTaskQuery : PaginationQuery
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public WorkTaskSourceType SourceType { get; init; } = WorkTaskSourceType.All;
    public Guid? WorkTaskListId { get; init; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public WorkTaskStatus? Status { get; init; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public WorkTaskPriority? Priority { get; init; }
    public bool OnlyOverdue { get; init; }
    public DateTime? DueFrom { get; init; }
    public DateTime? DueTo { get; init; }
    public bool IncludeCompleted { get; init; } = true;
    public bool IncludeInactive { get; init; }
}

public sealed class CreateMyTaskRequest
{
    public Guid? WorkTaskListId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? NextAction { get; init; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public WorkTaskPriority Priority { get; init; } = WorkTaskPriority.Normal;
    public DateTime? DueDate { get; init; }
    public int? SortOrder { get; init; }
}

public sealed class UpdateMyTaskRequest
{
    public Guid? WorkTaskListId { get; init; }
    public string? Title { get; init; }
    public string? Description { get; init; }
    public string? NextAction { get; init; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public WorkTaskStatus? Status { get; init; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public WorkTaskPriority? Priority { get; init; }
    public DateTime? DueDate { get; init; }
    public int? SortOrder { get; init; }
    public bool? IsActive { get; init; }
}

public sealed class CompleteMyTaskRequest
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public WorkTaskStatus Status { get; init; } = WorkTaskStatus.Done;
    public string? CompletionNote { get; init; }
}

public sealed class ReorderMyTaskRequest
{
    public IReadOnlyList<ReorderMyTaskItemDto> Items { get; init; } = [];
}

public sealed class ReorderMyTaskItemDto
{
    public Guid TaskId { get; init; }
    public Guid? WorkTaskListId { get; init; }
    public int SortOrder { get; init; }
}

public sealed class MyTaskDto
{
    public Guid WorkTaskId { get; init; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public WorkTaskSourceType SourceType { get; init; }
    public Guid? WorkTaskListId { get; init; }
    public int SortOrder { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? NextAction { get; init; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public WorkTaskStatus Status { get; init; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public WorkTaskPriority Priority { get; init; }
    public DateTime? DueDate { get; init; }
    public bool IsOverdue { get; init; }
    public Guid? AssignedEmployeeId { get; init; }
    public string? AssignedEmployeeName { get; init; }
    public Guid? CustomerId { get; init; }
    public string? CustomerExternalId { get; init; }
    public string? CustomerName { get; init; }
    public Guid? CustomerInteractionId { get; init; }
    public DateTime? CompletedDate { get; init; }
    public string? CompletionNote { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedDate { get; init; }
    public DateTime? UpdatedDate { get; init; }
}

public sealed class MyTaskBoardDto
{
    public IReadOnlyList<MyTaskListColumnDto> TaskLists { get; init; } = [];
    public IReadOnlyList<MyTaskDto> UnlistedTasks { get; init; } = [];
    public IReadOnlyList<MyTaskDto> CrmFollowUps { get; init; } = [];
}

public sealed class MyTaskListColumnDto
{
    public MyTaskListDto List { get; init; } = default!;
    public IReadOnlyList<MyTaskDto> Tasks { get; init; } = [];
}
