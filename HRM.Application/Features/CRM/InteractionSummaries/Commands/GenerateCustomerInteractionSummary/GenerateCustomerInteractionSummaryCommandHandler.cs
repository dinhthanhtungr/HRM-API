using HRM.Application.Abstractions.Commons.Ais.CRM;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Services;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HRM.Application.Features.CRM.InteractionSummaries.Dtos;
using HRM.Application.Features.CRM.InteractionSummaries.Services.GenerateCustomerInteractionSummary;

namespace HRM.Application.Features.CRM.InteractionSummaries.Commands.GenerateCustomerInteractionSummary;

/// <summary>
/// Xác thực customer/employee scope, lấy tối đa 80 interaction gần nhất, kiểm soát rate limit và lưu kết quả Gemini.
/// </summary>
internal sealed class GenerateCustomerInteractionSummaryCommandHandler
    : IRequestHandler<GenerateCustomerInteractionSummaryCommand, OperationResult<CustomerInteractionAiSummaryDto>>
{
    private const int MaxPromptInteractions = 80;
    private const string PromptVersion = "v1";
    private readonly ICRMReadDbContext _readDbContext;
    private readonly ICRMWriteDbContext _writeDbContext;
    private readonly CustomerCrmAccessService _accessService;
    private readonly ICustomerInteractionAiSummaryClient _client;
    private readonly IGeminiRateLimitService _rateLimitService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public GenerateCustomerInteractionSummaryCommandHandler(
        ICRMReadDbContext readDbContext,
        ICRMWriteDbContext writeDbContext,
        CustomerCrmAccessService accessService,
        ICustomerInteractionAiSummaryClient client,
        IGeminiRateLimitService rateLimitService,
        IDateTimeProvider dateTimeProvider)
    {
        _readDbContext = readDbContext;
        _writeDbContext = writeDbContext;
        _accessService = accessService;
        _client = client;
        _rateLimitService = rateLimitService;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <summary>
    /// Tạo summary theo kỳ; trả bản thành công đã có nếu không yêu cầu tạo lại và lưu trạng thái skipped/error để audit.
    /// </summary>
    public async Task<OperationResult<CustomerInteractionAiSummaryDto>> Handle(
        GenerateCustomerInteractionSummaryCommand command,
        CancellationToken cancellationToken)
    {
        if (command.CustomerId == Guid.Empty)
            return OperationResult<CustomerInteractionAiSummaryDto>.Fail("Customer id is required.");
        var request = command.Request;
        if (!Enum.IsDefined(request.SummaryScope))
            return OperationResult<CustomerInteractionAiSummaryDto>.Fail("Summary scope is invalid.");

        var scope = await _accessService.BuildScopeAsync(cancellationToken);
        var customer = await _accessService.VisibleCustomers(scope)
            .FirstOrDefaultAsync(x => x.CustomerId == command.CustomerId, cancellationToken);
        if (customer is null)
            return OperationResult<CustomerInteractionAiSummaryDto>.Fail("Customer was not found.");
        var employee = await _accessService.ResolveEmployeeAsync(scope, request.SaleEmployeeId, false, cancellationToken);
        if (!employee.IsAllowed)
            return OperationResult<CustomerInteractionAiSummaryDto>.Fail("Sale employee is outside your scope.");
        var period = ResolvePeriod(request, _dateTimeProvider.Now);
        if (!period.IsValid)
            return OperationResult<CustomerInteractionAiSummaryDto>.Fail(period.Error!);

        var existing = await _writeDbContext.CustomerInteractionAiSummaries.FirstOrDefaultAsync(x =>
            x.CustomerId == customer.CustomerId && x.CompanyId == scope.CompanyId && x.IsActive &&
            x.SummaryScope == request.SummaryScope && x.SaleEmployeeId == employee.EmployeeId &&
            x.PeriodFrom == period.From && x.PeriodTo == period.To, cancellationToken);
        if (existing is { IsAiSuccess: true } && !request.ForceRegenerate)
            return OperationResult<CustomerInteractionAiSummaryDto>.Ok(await MapAsync(existing, cancellationToken), "Existing summary returned.");

        var interactionQuery = _readDbContext.CustomerInteractions.AsNoTracking().Where(x =>
            x.CustomerId == customer.CustomerId && x.CompanyId == scope.CompanyId && x.IsActive &&
            x.InteractionAt >= period.From && x.InteractionAt <= period.To);
        if (employee.EmployeeId.HasValue) interactionQuery = interactionQuery.Where(x => x.AssignedSaleEmployeeId == employee.EmployeeId);
        var interactionCount = await interactionQuery.CountAsync(cancellationToken);
        var rows = await interactionQuery.OrderByDescending(x => x.InteractionAt).Take(MaxPromptInteractions)
            .Select(x => new CustomerInteractionPromptRow
            {
                InteractionAt = x.InteractionAt, InteractionType = x.InteractionType, Subject = x.Subject,
                Content = x.Content, Outcome = x.Outcome, NextAction = x.NextAction,
                AssignedSaleName = x.AssignedSaleEmployee != null ? x.AssignedSaleEmployee.FullName : null,
                CreatedByName = x.CreatedByNavigation.FullName
            }).ToListAsync(cancellationToken);
        var previousSummary = await _readDbContext.CustomerInteractionAiSummaries.AsNoTracking().Where(x =>
                x.CustomerId == customer.CustomerId && x.CompanyId == scope.CompanyId && x.IsActive && x.IsAiSuccess &&
                x.SaleEmployeeId == employee.EmployeeId && x.PeriodTo < period.From)
            .OrderByDescending(x => x.PeriodTo).Select(x => x.Summary).FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        var now = _dateTimeProvider.Now;
        var entity = existing ?? new CustomerInteractionAiSummary
        {
            Id = Guid.CreateVersion7(), CustomerId = customer.CustomerId, CompanyId = scope.CompanyId,
            CreatedDate = now, CreatedBy = scope.EmployeeId, IsActive = true
        };
        entity.SaleEmployeeId = employee.EmployeeId;
        entity.SummaryScope = request.SummaryScope;
        entity.Year = request.SummaryScope is CustomerInteractionSummaryScope.Monthly or CustomerInteractionSummaryScope.Yearly ? period.From.Year : null;
        entity.Month = request.SummaryScope == CustomerInteractionSummaryScope.Monthly ? period.From.Month : null;
        entity.PeriodFrom = period.From; entity.PeriodTo = period.To; entity.InteractionCount = interactionCount;
        entity.PreviousSummary = previousSummary; entity.SourceModel = _client.Model; entity.PromptVersion = PromptVersion;
        entity.UpdatedDate = existing is null ? null : now; entity.UpdatedBy = existing is null ? null : scope.EmployeeId;
        entity.AiErrorMessage = null; entity.IsAiSuccess = false; entity.IsAiSkipped = false;
        if (existing is null) await _writeDbContext.CustomerInteractionAiSummaries.AddAsync(entity, cancellationToken);

        if (interactionCount == 0)
        {
            entity.IsAiSkipped = true;
            entity.Summary = "No interactions were recorded in this period.";
            entity.CustomerNeed = entity.CurrentStage = entity.NextAction = entity.Risk = entity.Sentiment = string.Empty;
            await _writeDbContext.SaveChangesAsync(cancellationToken);
            return OperationResult<CustomerInteractionAiSummaryDto>.Ok(await MapAsync(entity, cancellationToken));
        }

        var rate = _rateLimitService.TryConsume(_client.Model);
        if (!rate.CanRequest)
            return OperationResult<CustomerInteractionAiSummaryDto>.Fail(rate.Message ?? "AI rate limit was reached.");
        try
        {
            var prompt = CustomerInteractionPromptBuilder.BuildSinglePrompt(customer, previousSummary, rows, request.SummaryScope, period.From, period.To);
            var result = await _client.GenerateSummaryAsync(prompt, cancellationToken);
            entity.Summary = result.Summary; entity.CustomerNeed = result.CustomerNeed; entity.CurrentStage = result.CurrentStage;
            entity.NextAction = result.NextAction; entity.Risk = result.Risk; entity.Sentiment = result.Sentiment;
            entity.IsAiSuccess = true; entity.AiGeneratedDate = _dateTimeProvider.Now;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            entity.AiErrorMessage = "AI summary generation failed.";
            entity.IsAiSuccess = false;
        }
        await _writeDbContext.SaveChangesAsync(cancellationToken);
        var dto = await MapAsync(entity, cancellationToken);
        dto.RateLimit = _rateLimitService.GetCurrent(_client.Model);
        return entity.IsAiSuccess
            ? OperationResult<CustomerInteractionAiSummaryDto>.Ok(dto)
            : OperationResult<CustomerInteractionAiSummaryDto>.Fail(dto, entity.AiErrorMessage!);
    }

    private async Task<CustomerInteractionAiSummaryDto> MapAsync(CustomerInteractionAiSummary entity, CancellationToken cancellationToken)
    {
        var names = await _readDbContext.Customers.AsNoTracking().Where(x => x.CustomerId == entity.CustomerId)
            .Select(x => new { x.ExternalId, x.CustomerName }).FirstAsync(cancellationToken);
        var sale = entity.SaleEmployeeId.HasValue
            ? await _readDbContext.Employees.AsNoTracking().Where(x => x.EmployeeId == entity.SaleEmployeeId)
                .Select(x => new { x.ExternalId, x.FullName }).FirstOrDefaultAsync(cancellationToken)
            : null;
        return new CustomerInteractionAiSummaryDto
        {
            Id = entity.Id, CustomerId = entity.CustomerId, CustomerCode = names.ExternalId, CustomerName = names.CustomerName,
            SaleEmployeeId = entity.SaleEmployeeId, SaleEmployeeCode = sale?.ExternalId, SaleEmployeeName = sale?.FullName,
            CompanyId = entity.CompanyId, SummaryScope = entity.SummaryScope, Year = entity.Year, Month = entity.Month,
            PeriodFrom = entity.PeriodFrom, PeriodTo = entity.PeriodTo, InteractionCount = entity.InteractionCount,
            PreviousSummary = entity.PreviousSummary, Summary = entity.Summary, CustomerNeed = entity.CustomerNeed,
            CurrentStage = entity.CurrentStage, NextAction = entity.NextAction, Risk = entity.Risk, Sentiment = entity.Sentiment,
            SourceModel = entity.SourceModel, PromptVersion = entity.PromptVersion, IsAiSuccess = entity.IsAiSuccess,
            IsAiSkipped = entity.IsAiSkipped, AiErrorMessage = entity.AiErrorMessage, AiGeneratedDate = entity.AiGeneratedDate,
            CreatedDate = entity.CreatedDate, UpdatedDate = entity.UpdatedDate, IsActive = entity.IsActive
        };
    }

    private static SummaryPeriod ResolvePeriod(GenerateCustomerInteractionAiSummaryRequest request, DateTime now)
    {
        return request.SummaryScope switch
        {
            CustomerInteractionSummaryScope.Monthly => ResolveMonth(request.Year ?? now.Year, request.Month ?? now.Month),
            CustomerInteractionSummaryScope.Yearly => ResolveYear(request.Year ?? now.Year),
            CustomerInteractionSummaryScope.Lifetime => SummaryPeriod.Valid(new DateTime(2000, 1, 1), now),
            CustomerInteractionSummaryScope.CustomRange when request.From.HasValue && request.To.HasValue && request.To.Value >= request.From.Value
                => SummaryPeriod.Valid(request.From.Value, request.To.Value.Date.AddDays(1).AddTicks(-1)),
            _ => SummaryPeriod.Invalid("Summary period is invalid.")
        };
    }

    private static SummaryPeriod ResolveMonth(int year, int month)
        => year is >= 2000 and <= 2100 && month is >= 1 and <= 12
            ? SummaryPeriod.Valid(new DateTime(year, month, 1), new DateTime(year, month, 1).AddMonths(1).AddTicks(-1))
            : SummaryPeriod.Invalid("Summary month is invalid.");

    private static SummaryPeriod ResolveYear(int year)
        => year is >= 2000 and <= 2100
            ? SummaryPeriod.Valid(new DateTime(year, 1, 1), new DateTime(year + 1, 1, 1).AddTicks(-1))
            : SummaryPeriod.Invalid("Summary year is invalid.");

    private sealed record SummaryPeriod(DateTime From, DateTime To, bool IsValid, string? Error)
    {
        public static SummaryPeriod Valid(DateTime from, DateTime to) => new(from, to, true, null);
        public static SummaryPeriod Invalid(string error) => new(default, default, false, error);
    }
}
