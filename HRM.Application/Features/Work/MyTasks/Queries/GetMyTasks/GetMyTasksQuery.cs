using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.Work.MyTasks.Dtos;
using HRM.Domain.Enums.WorkTaskEnums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HRM.Application.Commons.Searching;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Domain.Entities.CustomerSchema;

namespace HRM.Application.Features.Work.MyTasks.Queries.GetMyTasks;

public sealed class GetMyTasksQuery : IRequest<OperationResult<PagedResult<MyTaskDto>>>
{
    public MyTaskQuery Query { get; init; } = new();
}

internal sealed class GetMyTasksQueryHandler
    : IRequestHandler<GetMyTasksQuery, OperationResult<PagedResult<MyTaskDto>>>
{
    private readonly MyTaskSupport _support;
    private readonly ICustomerVisibilityReadDbContext _customerDb;
    private readonly ICustomerVisibilityService _customerVisibility;

    public GetMyTasksQueryHandler(MyTaskSupport support, ICustomerVisibilityReadDbContext customerDb, ICustomerVisibilityService customerVisibility)
    {
        _support = support;
        _customerDb = customerDb;
        _customerVisibility = customerVisibility;
    }

    public async Task<OperationResult<PagedResult<MyTaskDto>>> Handle(
        GetMyTasksQuery request,
        CancellationToken cancellationToken)
    {
        var actorResult = _support.ResolveActor();
        if (!actorResult.Success || actorResult.Data is null)
        {
            return OperationResult<PagedResult<MyTaskDto>>.Fail(actorResult.Message ?? MyTaskMessages.CurrentEmployeeOrCompanyInvalid);
        }

        var query = request.Query;
        var today = _support.Now.Date;
        var customers = _customerDb.Customers.AsNoTracking().Where(c => false);
        if (query.SuggestionKeyword is not null || !string.IsNullOrWhiteSpace(query.NormalizedKeyword) ||
            query.SearchSelections?.Contains("customerTaxNumber", StringComparison.Ordinal) == true)
        {
            var scope = await _customerVisibility.BuildReadScopeAsync(cancellationToken);
            if (scope.CompanyId == actorResult.Data.CompanyId)
                customers = _customerVisibility.ApplyCustomerVisibility(_customerDb.Customers.AsNoTracking(), scope)
                    .Where(c => c.CompanyId == actorResult.Data.CompanyId);
        }
        var source = BuildFilteredDtoQuery(actorResult.Data, query, today, customers);
        if (await MyTaskSmartSearch.Build(source, customers).SuggestionsAsync<MyTaskDto>(source, query, cancellationToken) is {} suggestions)
            return OperationResult<PagedResult<MyTaskDto>>.Ok(suggestions);
        var total = await source.CountAsync(cancellationToken);
        var items = await ApplySort(source, query)
            .Skip((query.NormalizedPageNumber - 1) * query.NormalizedPageSize)
            .Take(query.NormalizedPageSize)
            .ToListAsync(cancellationToken);

        return OperationResult<PagedResult<MyTaskDto>>.Ok(
            new PagedResult<MyTaskDto>(items, total, query.NormalizedPageNumber, query.NormalizedPageSize));
    }

    private IQueryable<MyTaskDto> BuildFilteredDtoQuery(MyTaskActor actor, MyTaskQuery query, DateTime today, IQueryable<Customer> customers)
    {
        IQueryable<MyTaskDto> source = query.SourceType switch
        {
            WorkTaskSourceType.Personal => _support.ProjectTasks(ApplyTaskFilters(_support.BuildPersonalTaskQuery(actor), query, today, true), today),
            WorkTaskSourceType.CustomerFollowUp => _support.ProjectTasks(ApplyTaskFilters(_support.BuildCustomerFollowUpTaskQuery(actor), query, today, false), today),
            _ when query.WorkTaskListId.HasValue => _support.ProjectTasks(ApplyTaskFilters(_support.BuildPersonalTaskQuery(actor), query, today, true), today),
            _ => _support.ProjectTasks(ApplyTaskFilters(_support.BuildPersonalTaskQuery(actor), query, today, true), today)
                .Concat(_support.ProjectTasks(ApplyTaskFilters(_support.BuildCustomerFollowUpTaskQuery(actor), query, today, false), today))
        };

        source = MyTaskSmartSearch.Build(source, customers).ApplyRequest(source, query.NormalizedKeyword, query, out var smartKeyword);
        if (!string.IsNullOrWhiteSpace(smartKeyword))
        {
            var keyword = smartKeyword;
            source = source.Where(x =>
                EF.Functions.ILike(x.Title, PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike((x.Description ?? string.Empty), PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike((x.CustomerName ?? string.Empty), PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike((x.CustomerExternalId ?? string.Empty), PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter) ||
                customers.Any(c => c.CustomerId == x.CustomerId && EF.Functions.ILike(c.TaxNumber ?? "", PostgresSearchPattern.ContainsLiteral(keyword), PostgresSearchPattern.EscapeCharacter)));
        }

        return source;
    }

    private static IQueryable<HRM.Domain.Entities.WorkTaskSchema.WorkTask> ApplyTaskFilters(
        IQueryable<HRM.Domain.Entities.WorkTaskSchema.WorkTask> source,
        MyTaskQuery query,
        DateTime today,
        bool allowListFilter)
    {
        if (!query.IncludeInactive)
        {
            source = source.Where(x => x.IsActive);
        }

        if (!query.IncludeCompleted)
        {
            source = source.Where(x => x.Status != WorkTaskStatus.Done && x.Status != WorkTaskStatus.Canceled);
        }

        if (allowListFilter && query.WorkTaskListId.HasValue)
        {
            source = source.Where(x => x.WorkTaskListId == query.WorkTaskListId.Value);
        }

        if (query.Status.HasValue)
        {
            source = source.Where(x => x.Status == query.Status);
        }

        if (query.Priority.HasValue)
        {
            source = source.Where(x => x.Priority == query.Priority);
        }

        if (query.OnlyOverdue)
        {
            source = source.Where(x => x.DueDate.HasValue && x.DueDate.Value.Date < today && x.Status != WorkTaskStatus.Done && x.Status != WorkTaskStatus.Canceled);
        }

        if (query.DueFrom.HasValue)
        {
            source = source.Where(x => x.DueDate >= query.DueFrom.Value.Date);
        }

        if (query.DueTo.HasValue)
        {
            source = source.Where(x => x.DueDate < query.DueTo.Value.Date.AddDays(1));
        }

        return source;
    }

    private static IQueryable<MyTaskDto> ApplySort(IQueryable<MyTaskDto> source, MyTaskQuery query)
        => query.NormalizedSortBy?.ToLowerInvariant() switch
        {
            MyTaskSortFields.Title => query.SortDescending
                ? source.OrderByDescending(x => x.Title).ThenByDescending(x => x.WorkTaskId)
                : source.OrderBy(x => x.Title).ThenByDescending(x => x.WorkTaskId),
            MyTaskSortFields.Priority => query.SortDescending
                ? source.OrderByDescending(x => x.Priority).ThenBy(x => x.DueDate == null).ThenBy(x => x.DueDate)
                : source.OrderBy(x => x.Priority).ThenBy(x => x.DueDate == null).ThenBy(x => x.DueDate),
            MyTaskSortFields.Status => query.SortDescending
                ? source.OrderByDescending(x => x.Status).ThenBy(x => x.DueDate == null).ThenBy(x => x.DueDate)
                : source.OrderBy(x => x.Status).ThenBy(x => x.DueDate == null).ThenBy(x => x.DueDate),
            MyTaskSortFields.CreatedDate => query.SortDescending
                ? source.OrderByDescending(x => x.CreatedDate)
                : source.OrderBy(x => x.CreatedDate),
            MyTaskSortFields.SortOrder => query.SortDescending
                ? source.OrderByDescending(x => x.SourceType).ThenByDescending(x => x.SortOrder).ThenByDescending(x => x.WorkTaskId)
                : source.OrderBy(x => x.SourceType).ThenBy(x => x.SortOrder).ThenByDescending(x => x.WorkTaskId),
            _ => source.OrderByDescending(x => x.IsOverdue)
                .ThenBy(x => x.SourceType)
                .ThenBy(x => x.WorkTaskListId == null)
                .ThenBy(x => x.SortOrder)
                .ThenBy(x => x.DueDate == null)
                .ThenBy(x => x.DueDate)
                .ThenByDescending(x => x.WorkTaskId)
        };
}

internal static class MyTaskSortFields
{
    public const string Title = "title";
    public const string Priority = "priority";
    public const string Status = "status";
    public const string CreatedDate = "createddate";
    public const string SortOrder = "sortorder";
}
