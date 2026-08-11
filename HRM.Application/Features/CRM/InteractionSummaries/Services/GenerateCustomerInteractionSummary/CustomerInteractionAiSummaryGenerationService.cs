using HRM.Application.Abstractions.Commons.Ais.CRM;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Concurrency;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.InteractionSummaries.Dtos;
using HRM.Application.Features.CRM.InteractionSummaries.Models.GenerateCustomerInteractionSummary;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HRM.Application.Features.CRM.InteractionSummaries.Services.GenerateCustomerInteractionSummary;

/// <summary>
/// Tạo và cache AI summary theo kỳ dữ liệu. Service không phụ thuộc current user để API có phân quyền
/// và background worker có thể dùng chung đúng một rule sinh summary.
/// </summary>
internal sealed class CustomerInteractionAiSummaryGenerationService
{
    private const int MaxPromptInteractions = 80;
    internal const string CurrentPromptVersion = "v3";
    private static readonly DateTime LifetimeStart = new(2000, 1, 1);

    private readonly ICRMReadDbContext _readDbContext;
    private readonly ICRMWriteDbContext _writeDbContext;
    private readonly ICustomerInteractionAiSummaryClient _client;
    private readonly IGeminiRateLimitService _rateLimitService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly KeyedMutationLock<Guid> _mutationLock;
    private readonly ILogger<CustomerInteractionAiSummaryGenerationService> _logger;

    public CustomerInteractionAiSummaryGenerationService(
        ICRMReadDbContext readDbContext,
        ICRMWriteDbContext writeDbContext,
        ICustomerInteractionAiSummaryClient client,
        IGeminiRateLimitService rateLimitService,
        IDateTimeProvider dateTimeProvider,
        KeyedMutationLock<Guid> mutationLock,
        ILogger<CustomerInteractionAiSummaryGenerationService> logger)
    {
        _readDbContext = readDbContext;
        _writeDbContext = writeDbContext;
        _client = client;
        _rateLimitService = rateLimitService;
        _dateTimeProvider = dateTimeProvider;
        _mutationLock = mutationLock;
        _logger = logger;
    }

    public async Task<CustomerInteractionAiSummaryGenerationOutcome> GenerateAsync(
        Guid customerId,
        Guid companyId,
        Guid? saleEmployeeId,
        Guid auditEmployeeId,
        GenerateCustomerInteractionAiSummaryRequest request,
        CancellationToken cancellationToken)
    {
        using var mutationLock = await _mutationLock.AcquireAsync(customerId, cancellationToken);

        var customer = await _readDbContext.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.CustomerId == customerId &&
                x.CompanyId == companyId &&
                x.IsActive != false,
                cancellationToken);
        if (customer is null)
        {
            return CustomerInteractionAiSummaryGenerationOutcome.NotRequested(
                OperationResult<CustomerInteractionAiSummaryDto>.Fail("Customer was not found."));
        }

        var period = ResolvePeriod(request, _dateTimeProvider.Now);
        if (!period.IsValid)
        {
            return CustomerInteractionAiSummaryGenerationOutcome.NotRequested(
                OperationResult<CustomerInteractionAiSummaryDto>.Fail(period.Error!));
        }

        if (request.SummaryScope is CustomerInteractionSummaryScope.Yearly or CustomerInteractionSummaryScope.Lifetime)
        {
            return await GenerateRollupAsync(
                customer,
                companyId,
                saleEmployeeId,
                auditEmployeeId,
                request,
                period,
                cancellationToken);
        }

        var periodSourceQuery = _readDbContext.CustomerInteractions
            .AsNoTracking()
            .Where(x =>
                x.CustomerId == customerId &&
                x.CompanyId == companyId &&
                x.InteractionAt >= period.From &&
                x.InteractionAt <= period.To);
        if (saleEmployeeId.HasValue)
        {
            periodSourceQuery = periodSourceQuery.Where(x => x.AssignedSaleEmployeeId == saleEmployeeId);
        }

        var interactionQuery = periodSourceQuery.Where(x => x.IsActive);
        var stats = await interactionQuery
            .GroupBy(_ => 1)
            .Select(group => new InteractionPeriodStats(
                group.Count(),
                group.Max(x => x.InteractionAt)))
            .FirstOrDefaultAsync(cancellationToken);
        var latestSourceChangedAt = await periodSourceQuery
            .Select(x => (DateTime?)(x.UpdatedDate ?? x.CreatedDate))
            .MaxAsync(cancellationToken);

        var existing = await _writeDbContext.CustomerInteractionAiSummaries
            .FirstOrDefaultAsync(x =>
                x.CustomerId == customerId &&
                x.CompanyId == companyId &&
                x.IsActive &&
                x.SummaryScope == request.SummaryScope &&
                x.SaleEmployeeId == saleEmployeeId &&
                x.PeriodFrom == period.From &&
                x.PeriodTo == period.To,
                cancellationToken);

        if (CanReuse(existing, stats, latestSourceChangedAt, request.ForceRegenerate))
        {
            var cachedDto = await MapAsync(existing!, cancellationToken);
            cachedDto.RateLimit = _rateLimitService.GetCurrent(_client.Model);
            return CustomerInteractionAiSummaryGenerationOutcome.NotRequested(
                OperationResult<CustomerInteractionAiSummaryDto>.Ok(cachedDto, "Existing summary returned."));
        }

        var now = _dateTimeProvider.Now;
        var entity = existing ?? new CustomerInteractionAiSummary
        {
            Id = Guid.CreateVersion7(),
            CustomerId = customerId,
            CompanyId = companyId,
            CreatedDate = now,
            CreatedBy = auditEmployeeId,
            IsActive = true
        };

        entity.SaleEmployeeId = saleEmployeeId;
        entity.SummaryScope = request.SummaryScope;
        entity.Year = request.SummaryScope is CustomerInteractionSummaryScope.Monthly or CustomerInteractionSummaryScope.Yearly
            ? period.From.Year
            : null;
        entity.Month = request.SummaryScope == CustomerInteractionSummaryScope.Monthly ? period.From.Month : null;
        entity.PeriodFrom = period.From;
        entity.PeriodTo = period.To;
        entity.InteractionCount = stats?.InteractionCount ?? 0;
        entity.SourceModel = _client.Model;
        entity.PromptVersion = CurrentPromptVersion;
        entity.UpdatedDate = existing is null ? null : now;
        entity.UpdatedBy = existing is null ? null : auditEmployeeId;
        entity.AiErrorMessage = null;
        entity.IsAiSuccess = false;
        entity.IsAiSkipped = false;

        if (existing is null)
        {
            await _writeDbContext.CustomerInteractionAiSummaries.AddAsync(entity, cancellationToken);
        }

        if (stats is null)
        {
            entity.PreviousSummary = string.Empty;
            entity.IsAiSkipped = true;
            entity.Summary = "No interactions were recorded in this period.";
            entity.CustomerNeed = string.Empty;
            entity.CurrentStage = string.Empty;
            entity.NextAction = string.Empty;
            entity.Risk = string.Empty;
            entity.Sentiment = string.Empty;
            await _writeDbContext.SaveChangesAsync(cancellationToken);
            return CustomerInteractionAiSummaryGenerationOutcome.NotRequested(
                OperationResult<CustomerInteractionAiSummaryDto>.Ok(await MapAsync(entity, cancellationToken)));
        }

        var rows = await interactionQuery
            .OrderByDescending(x => x.InteractionAt)
            .Take(MaxPromptInteractions)
            .Select(x => new CustomerInteractionPromptRow
            {
                InteractionAt = x.InteractionAt,
                InteractionType = x.InteractionType,
                Subject = x.Subject,
                Content = x.Content,
                Outcome = x.Outcome,
                NextAction = x.NextAction,
                AssignedSaleName = x.AssignedSaleEmployee != null ? x.AssignedSaleEmployee.FullName : null,
                CreatedByName = x.CreatedByNavigation.FullName
            })
            .ToListAsync(cancellationToken);

        // Monthly/CustomRange chỉ dùng dữ liệu đúng kỳ được chọn, không mang summary kỳ trước vào prompt.
        entity.PreviousSummary = string.Empty;

        var rate = _rateLimitService.TryConsume(_client.Model);
        if (!rate.CanRequest)
        {
            return CustomerInteractionAiSummaryGenerationOutcome.NotRequested(
                OperationResult<CustomerInteractionAiSummaryDto>.Fail(rate.Message ?? "AI rate limit was reached."));
        }

        try
        {
            var prompt = CustomerInteractionPromptBuilder.BuildSinglePrompt(
                customer,
                entity.PreviousSummary,
                rows,
                request.SummaryScope,
                period.From,
                period.To);
            var result = await _client.GenerateSummaryAsync(prompt, cancellationToken);
            if (string.IsNullOrWhiteSpace(result.Summary))
            {
                throw new InvalidOperationException("AI returned an empty summary.");
            }

            entity.Summary = result.Summary;
            entity.CustomerNeed = result.CustomerNeed;
            entity.CurrentStage = result.CurrentStage;
            entity.NextAction = result.NextAction;
            entity.Risk = result.Risk;
            entity.Sentiment = result.Sentiment;
            entity.IsAiSuccess = true;
            entity.AiGeneratedDate = _dateTimeProvider.Now;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            entity.AiErrorMessage = "AI summary timed out. Please retry or choose a shorter period.";
            entity.IsAiSuccess = false;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(
                exception,
                "Gemini AI customer summary failed for customer {CustomerId}, scope {SummaryScope}, year {Year}, month {Month}.",
                customerId,
                request.SummaryScope,
                request.Year,
                request.Month);
            entity.AiErrorMessage = "AI summary generation failed. Please retry.";
            entity.IsAiSuccess = false;
        }

        await _writeDbContext.SaveChangesAsync(cancellationToken);
        var dto = await MapAsync(entity, cancellationToken);
        dto.RateLimit = _rateLimitService.GetCurrent(_client.Model);
        var operation = entity.IsAiSuccess
            ? OperationResult<CustomerInteractionAiSummaryDto>.Ok(dto)
            : OperationResult<CustomerInteractionAiSummaryDto>.Fail(dto, entity.AiErrorMessage!);
        return CustomerInteractionAiSummaryGenerationOutcome.Requested(operation);
    }

    /// <summary>
    /// Sinh tối đa năm summary tháng bằng một request Gemini. Kết quả vẫn được kiểm tra và lưu
    /// độc lập theo từng khách; cache và kỳ không có interaction không tiêu tốn request AI.
    /// Chỉ background automation dùng method này, API tạo thủ công vẫn gọi GenerateAsync.
    /// </summary>
    public async Task<CustomerInteractionAiSummaryGenerationBatchOutcome> GenerateMonthlyBatchAsync(
        IReadOnlyList<CustomerInteractionAiSummaryGenerationBatchRequest> requests,
        int maxCustomers,
        CancellationToken cancellationToken)
    {
        var normalizedRequests = requests
            .Where(x => x.CustomerId != Guid.Empty && x.CompanyId != Guid.Empty)
            .Take(Math.Clamp(maxCustomers, 1, 5))
            .ToArray();
        if (normalizedRequests.Length == 0)
        {
            return CustomerInteractionAiSummaryGenerationBatchOutcome.NotRequested([]);
        }

        var outcomes = new List<CustomerInteractionAiSummaryGenerationBatchItemOutcome>();
        var pending = new List<PendingMonthlySummary>();

        foreach (var batchRequest in normalizedRequests)
        {
            var request = new GenerateCustomerInteractionAiSummaryRequest
            {
                SummaryScope = CustomerInteractionSummaryScope.Monthly,
                Year = batchRequest.Year,
                Month = batchRequest.Month,
                ForceRegenerate = false
            };
            var period = ResolvePeriod(request, _dateTimeProvider.Now);
            if (!period.IsValid)
            {
                outcomes.Add(FailedBatchItem(batchRequest, period.Error!));
                continue;
            }

            var customer = await _readDbContext.Customers
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.CustomerId == batchRequest.CustomerId &&
                    x.CompanyId == batchRequest.CompanyId &&
                    x.IsActive != false,
                    cancellationToken);
            if (customer is null)
            {
                outcomes.Add(FailedBatchItem(batchRequest, "Customer was not found."));
                continue;
            }

            var periodSourceQuery = _readDbContext.CustomerInteractions
                .AsNoTracking()
                .Where(x =>
                    x.CustomerId == batchRequest.CustomerId &&
                    x.CompanyId == batchRequest.CompanyId &&
                    x.InteractionAt >= period.From &&
                    x.InteractionAt <= period.To);
            var interactionQuery = periodSourceQuery.Where(x => x.IsActive);
            var stats = await interactionQuery
                .GroupBy(_ => 1)
                .Select(group => new InteractionPeriodStats(group.Count(), group.Max(x => x.InteractionAt)))
                .FirstOrDefaultAsync(cancellationToken);
            var latestSourceChangedAt = await periodSourceQuery
                .Select(x => (DateTime?)(x.UpdatedDate ?? x.CreatedDate))
                .MaxAsync(cancellationToken);
            var existing = await _writeDbContext.CustomerInteractionAiSummaries
                .FirstOrDefaultAsync(x =>
                    x.CustomerId == batchRequest.CustomerId &&
                    x.CompanyId == batchRequest.CompanyId &&
                    x.IsActive &&
                    x.SummaryScope == CustomerInteractionSummaryScope.Monthly &&
                    x.SaleEmployeeId == null &&
                    x.PeriodFrom == period.From &&
                    x.PeriodTo == period.To,
                    cancellationToken);

            if (CanReuse(existing, stats, latestSourceChangedAt, false))
            {
                var cachedDto = await MapAsync(existing!, cancellationToken);
                cachedDto.RateLimit = _rateLimitService.GetCurrent(_client.Model);
                outcomes.Add(new(batchRequest, CustomerInteractionAiSummaryGenerationOutcome.NotRequested(
                    OperationResult<CustomerInteractionAiSummaryDto>.Ok(cachedDto, "Existing summary returned."))));
                continue;
            }

            var entity = existing ?? new CustomerInteractionAiSummary
            {
                Id = Guid.CreateVersion7(),
                CustomerId = batchRequest.CustomerId,
                CompanyId = batchRequest.CompanyId,
                CreatedDate = _dateTimeProvider.Now,
                CreatedBy = batchRequest.AuditEmployeeId,
                IsActive = true
            };
            ConfigureMonthlyEntity(entity, existing is null, batchRequest.AuditEmployeeId, period, stats?.InteractionCount ?? 0);

            if (stats is null)
            {
                if (existing is null)
                {
                    await _writeDbContext.CustomerInteractionAiSummaries.AddAsync(entity, cancellationToken);
                }

                entity.PreviousSummary = string.Empty;
                entity.IsAiSkipped = true;
                entity.Summary = "No interactions were recorded in this period.";
                entity.CustomerNeed = string.Empty;
                entity.CurrentStage = string.Empty;
                entity.NextAction = string.Empty;
                entity.Risk = string.Empty;
                entity.Sentiment = string.Empty;
                await _writeDbContext.SaveChangesAsync(cancellationToken);
                outcomes.Add(new(batchRequest, CustomerInteractionAiSummaryGenerationOutcome.NotRequested(
                    OperationResult<CustomerInteractionAiSummaryDto>.Ok(await MapAsync(entity, cancellationToken)))));
                continue;
            }

            var rows = await interactionQuery
                .OrderByDescending(x => x.InteractionAt)
                .Take(MaxPromptInteractions)
                .Select(x => new CustomerInteractionPromptRow
                {
                    InteractionAt = x.InteractionAt,
                    InteractionType = x.InteractionType,
                    Subject = x.Subject,
                    Content = x.Content,
                    Outcome = x.Outcome,
                    NextAction = x.NextAction,
                    AssignedSaleName = x.AssignedSaleEmployee != null ? x.AssignedSaleEmployee.FullName : null,
                    CreatedByName = x.CreatedByNavigation.FullName
                })
                .ToListAsync(cancellationToken);
            pending.Add(new(batchRequest, customer, period, entity, existing is null, rows));
        }

        if (pending.Count == 0)
        {
            return CustomerInteractionAiSummaryGenerationBatchOutcome.NotRequested(outcomes);
        }

        var rate = _rateLimitService.TryConsume(_client.Model);
        if (!rate.CanRequest)
        {
            foreach (var item in pending)
            {
                outcomes.Add(new(item.Request, CustomerInteractionAiSummaryGenerationOutcome.NotRequested(
                    OperationResult<CustomerInteractionAiSummaryDto>.Fail(
                        rate.Message ?? "AI rate limit was reached."))));
            }

            return CustomerInteractionAiSummaryGenerationBatchOutcome.NotRequested(outcomes);
        }

        foreach (var item in pending.Where(x => x.IsNew))
        {
            await _writeDbContext.CustomerInteractionAiSummaries.AddAsync(item.Entity, cancellationToken);
        }

        try
        {
            var prompt = CustomerInteractionPromptBuilder.BuildMonthlyBatchPrompt(pending
                .Select(x => new CustomerInteractionBatchPromptItem
                {
                    CustomerId = x.Request.CustomerId,
                    CustomerCode = x.Customer.ExternalId,
                    CustomerName = x.Customer.CustomerName,
                    PeriodFrom = x.Period.From,
                    PeriodTo = x.Period.To,
                    Interactions = x.Rows
                })
                .ToArray());
            var batchResult = await _client.GenerateBatchSummaryAsync(prompt, cancellationToken);
            var resultsByCustomerId = batchResult.Items
                .GroupBy(x => x.CustomerId)
                .ToDictionary(x => x.Key, x => x.First());

            foreach (var item in pending)
            {
                if (!resultsByCustomerId.TryGetValue(item.Request.CustomerId, out var result) ||
                    string.IsNullOrWhiteSpace(result.Summary))
                {
                    item.Entity.AiErrorMessage = string.IsNullOrWhiteSpace(result?.ErrorMessage)
                        ? "AI batch response did not contain a valid summary for this customer."
                        : LimitText(result.ErrorMessage, 500);
                    item.Entity.IsAiSuccess = false;
                    continue;
                }

                ApplyAiResult(item.Entity, result);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            foreach (var item in pending)
            {
                item.Entity.AiErrorMessage = "AI summary timed out. Please retry or choose a shorter period.";
                item.Entity.IsAiSuccess = false;
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Gemini AI customer summary batch failed for {CustomerCount} monthly customers.", pending.Count);
            foreach (var item in pending)
            {
                item.Entity.AiErrorMessage = "AI summary generation failed. Please retry.";
                item.Entity.IsAiSuccess = false;
            }
        }

        await _writeDbContext.SaveChangesAsync(cancellationToken);
        foreach (var item in pending)
        {
            var dto = await MapAsync(item.Entity, cancellationToken);
            dto.RateLimit = _rateLimitService.GetCurrent(_client.Model);
            var operation = item.Entity.IsAiSuccess
                ? OperationResult<CustomerInteractionAiSummaryDto>.Ok(dto)
                : OperationResult<CustomerInteractionAiSummaryDto>.Fail(dto, item.Entity.AiErrorMessage!);
            outcomes.Add(new(item.Request, CustomerInteractionAiSummaryGenerationOutcome.Requested(operation)));
        }

        return CustomerInteractionAiSummaryGenerationBatchOutcome.Requested(outcomes);
    }

    private void ConfigureMonthlyEntity(
        CustomerInteractionAiSummary entity,
        bool isNew,
        Guid auditEmployeeId,
        SummaryPeriod period,
        int interactionCount)
    {
        var now = _dateTimeProvider.Now;
        entity.SaleEmployeeId = null;
        entity.SummaryScope = CustomerInteractionSummaryScope.Monthly;
        entity.Year = period.From.Year;
        entity.Month = period.From.Month;
        entity.PeriodFrom = period.From;
        entity.PeriodTo = period.To;
        entity.InteractionCount = interactionCount;
        entity.SourceModel = _client.Model;
        entity.PromptVersion = CurrentPromptVersion;
        entity.UpdatedDate = isNew ? null : now;
        entity.UpdatedBy = isNew ? null : auditEmployeeId;
        entity.AiErrorMessage = null;
        entity.IsAiSuccess = false;
        entity.IsAiSkipped = false;
    }

    private void ApplyAiResult(CustomerInteractionAiSummary entity, CustomerInteractionAiSummaryResult result)
    {
        entity.Summary = result.Summary;
        entity.CustomerNeed = result.CustomerNeed;
        entity.CurrentStage = result.CurrentStage;
        entity.NextAction = result.NextAction;
        entity.Risk = result.Risk;
        entity.Sentiment = result.Sentiment;
        entity.IsAiSuccess = true;
        entity.IsAiSkipped = false;
        entity.AiErrorMessage = null;
        entity.AiGeneratedDate = _dateTimeProvider.Now;
    }

    private static string LimitText(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength] + "...";

    private static CustomerInteractionAiSummaryGenerationBatchItemOutcome FailedBatchItem(
        CustomerInteractionAiSummaryGenerationBatchRequest request,
        string message)
        => new(request, CustomerInteractionAiSummaryGenerationOutcome.NotRequested(
            OperationResult<CustomerInteractionAiSummaryDto>.Fail(message)));

    private async Task<CustomerInteractionAiSummaryGenerationOutcome> GenerateRollupAsync(
        Customer customer,
        Guid companyId,
        Guid? saleEmployeeId,
        Guid auditEmployeeId,
        GenerateCustomerInteractionAiSummaryRequest request,
        SummaryPeriod requestedPeriod,
        CancellationToken cancellationToken)
    {
        var source = request.SummaryScope == CustomerInteractionSummaryScope.Yearly
            ? await LoadYearlySourceAsync(
                customer.CustomerId,
                companyId,
                saleEmployeeId,
                requestedPeriod,
                cancellationToken)
            : await LoadLifetimeSourceAsync(
                customer.CustomerId,
                companyId,
                saleEmployeeId,
                requestedPeriod,
                cancellationToken);

        if (!source.IsReady)
        {
            return CustomerInteractionAiSummaryGenerationOutcome.NotRequested(
                OperationResult<CustomerInteractionAiSummaryDto>.Fail(source.Error!));
        }

        var period = request.SummaryScope == CustomerInteractionSummaryScope.Lifetime
            ? SummaryPeriod.Valid(LifetimeStart, source.LatestInteractionAt ?? LifetimeStart)
            : requestedPeriod;
        var existing = await _writeDbContext.CustomerInteractionAiSummaries
            .FirstOrDefaultAsync(x =>
                x.CustomerId == customer.CustomerId &&
                x.CompanyId == companyId &&
                x.IsActive &&
                x.SummaryScope == request.SummaryScope &&
                x.SaleEmployeeId == saleEmployeeId &&
                x.PeriodFrom == period.From &&
                x.PeriodTo == period.To,
                cancellationToken);

        if (CanReuseRollup(existing, source, request.ForceRegenerate))
        {
            var cachedDto = await MapAsync(existing!, cancellationToken);
            cachedDto.RateLimit = _rateLimitService.GetCurrent(_client.Model);
            return CustomerInteractionAiSummaryGenerationOutcome.NotRequested(
                OperationResult<CustomerInteractionAiSummaryDto>.Ok(cachedDto, "Existing summary returned."));
        }

        var now = _dateTimeProvider.Now;
        var entity = existing ?? new CustomerInteractionAiSummary
        {
            Id = Guid.CreateVersion7(),
            CustomerId = customer.CustomerId,
            CompanyId = companyId,
            CreatedDate = now,
            CreatedBy = auditEmployeeId,
            IsActive = true
        };

        entity.SaleEmployeeId = saleEmployeeId;
        entity.SummaryScope = request.SummaryScope;
        entity.Year = request.SummaryScope == CustomerInteractionSummaryScope.Yearly ? period.From.Year : null;
        entity.Month = null;
        entity.PeriodFrom = period.From;
        entity.PeriodTo = period.To;
        entity.InteractionCount = source.InteractionCount;
        entity.PreviousSummary = string.Empty;
        entity.SourceModel = _client.Model;
        entity.PromptVersion = CurrentPromptVersion;
        entity.UpdatedDate = existing is null ? null : now;
        entity.UpdatedBy = existing is null ? null : auditEmployeeId;
        entity.AiErrorMessage = null;
        entity.IsAiSuccess = false;
        entity.IsAiSkipped = false;

        if (existing is null)
        {
            await _writeDbContext.CustomerInteractionAiSummaries.AddAsync(entity, cancellationToken);
        }

        if (source.Items.Count == 0)
        {
            entity.IsAiSkipped = true;
            entity.Summary = "No interactions were recorded in this period.";
            entity.CustomerNeed = string.Empty;
            entity.CurrentStage = string.Empty;
            entity.NextAction = string.Empty;
            entity.Risk = string.Empty;
            entity.Sentiment = string.Empty;
            await _writeDbContext.SaveChangesAsync(cancellationToken);
            return CustomerInteractionAiSummaryGenerationOutcome.NotRequested(
                OperationResult<CustomerInteractionAiSummaryDto>.Ok(await MapAsync(entity, cancellationToken)));
        }

        var rate = _rateLimitService.TryConsume(_client.Model);
        if (!rate.CanRequest)
        {
            return CustomerInteractionAiSummaryGenerationOutcome.NotRequested(
                OperationResult<CustomerInteractionAiSummaryDto>.Fail(
                    rate.Message ?? "AI rate limit was reached."));
        }

        try
        {
            var prompt = CustomerInteractionPromptBuilder.BuildRollupPrompt(
                customer,
                source.Items,
                request.SummaryScope,
                period.From,
                period.To);
            var result = await _client.GenerateSummaryAsync(prompt, cancellationToken);
            if (string.IsNullOrWhiteSpace(result.Summary))
            {
                throw new InvalidOperationException("AI returned an empty summary.");
            }

            entity.Summary = result.Summary;
            entity.CustomerNeed = result.CustomerNeed;
            entity.CurrentStage = result.CurrentStage;
            entity.NextAction = result.NextAction;
            entity.Risk = result.Risk;
            entity.Sentiment = result.Sentiment;
            entity.IsAiSuccess = true;
            entity.AiGeneratedDate = _dateTimeProvider.Now;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            entity.AiErrorMessage = "AI summary timed out. Please retry.";
            entity.IsAiSuccess = false;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            entity.AiErrorMessage = "AI summary generation failed. Please retry.";
            entity.IsAiSuccess = false;
        }

        await _writeDbContext.SaveChangesAsync(cancellationToken);
        var dto = await MapAsync(entity, cancellationToken);
        dto.RateLimit = _rateLimitService.GetCurrent(_client.Model);
        var operation = entity.IsAiSuccess
            ? OperationResult<CustomerInteractionAiSummaryDto>.Ok(dto)
            : OperationResult<CustomerInteractionAiSummaryDto>.Fail(dto, entity.AiErrorMessage!);
        return CustomerInteractionAiSummaryGenerationOutcome.Requested(operation);
    }

    private async Task<RollupSource> LoadYearlySourceAsync(
        Guid customerId,
        Guid companyId,
        Guid? saleEmployeeId,
        SummaryPeriod period,
        CancellationToken cancellationToken)
    {
        var interactionQuery = _readDbContext.CustomerInteractions
            .AsNoTracking()
            .Where(x =>
                x.CustomerId == customerId &&
                x.CompanyId == companyId &&
                x.IsActive &&
                x.InteractionAt >= period.From &&
                x.InteractionAt <= period.To);
        if (saleEmployeeId.HasValue)
        {
            interactionQuery = interactionQuery.Where(x => x.AssignedSaleEmployeeId == saleEmployeeId);
        }

        var monthSourceRows = await interactionQuery
            .GroupBy(x => x.InteractionAt.Month)
            .Select(group => new
            {
                Key = group.Key,
                InteractionCount = group.Count(),
                LatestInteractionAt = group.Max(x => x.InteractionAt),
                LatestSourceChangedAt = group.Max(x => x.UpdatedDate ?? x.CreatedDate)
            })
            .OrderBy(x => x.Key)
            .ToListAsync(cancellationToken);
        var monthSources = monthSourceRows
            .Select(x => new RollupDependencySource(
                x.Key,
                x.InteractionCount,
                x.LatestInteractionAt,
                x.LatestSourceChangedAt))
            .ToList();
        if (monthSources.Count == 0)
        {
            return RollupSource.Ready([], 0, null, null);
        }

        var monthlySummaries = await _readDbContext.CustomerInteractionAiSummaries
            .AsNoTracking()
            .Where(x =>
                x.CustomerId == customerId &&
                x.CompanyId == companyId &&
                x.SaleEmployeeId == saleEmployeeId &&
                x.IsActive &&
                x.SummaryScope == CustomerInteractionSummaryScope.Monthly &&
                x.Year == period.From.Year)
            .OrderByDescending(x => x.AiGeneratedDate ?? x.UpdatedDate ?? x.CreatedDate)
            .ToListAsync(cancellationToken);

        var items = new List<CustomerInteractionSummaryRollupItem>();
        var unavailableMonths = new List<int>();
        DateTime? latestSummaryChangedAt = null;
        foreach (var monthSource in monthSources)
        {
            var summary = monthlySummaries.FirstOrDefault(x => x.Month == monthSource.Key);
            if (!IsDependencyReady(summary, monthSource.LatestSourceChangedAt))
            {
                unavailableMonths.Add(monthSource.Key);
                continue;
            }

            items.Add(MapRollupItem($"Tháng {monthSource.Key:00}/{period.From.Year}", summary!));
            latestSummaryChangedAt = Max(
                latestSummaryChangedAt,
                summary!.AiGeneratedDate ?? summary.UpdatedDate ?? summary.CreatedDate);
        }

        return unavailableMonths.Count > 0
            ? RollupSource.NotReady(
                $"Monthly summaries are not ready for months: {string.Join(", ", unavailableMonths.Select(x => x.ToString("00")))}.")
            : RollupSource.Ready(
                items,
                monthSources.Sum(x => x.InteractionCount),
                monthSources.Max(x => x.LatestInteractionAt),
                latestSummaryChangedAt);
    }

    private async Task<RollupSource> LoadLifetimeSourceAsync(
        Guid customerId,
        Guid companyId,
        Guid? saleEmployeeId,
        SummaryPeriod period,
        CancellationToken cancellationToken)
    {
        var interactionQuery = _readDbContext.CustomerInteractions
            .AsNoTracking()
            .Where(x =>
                x.CustomerId == customerId &&
                x.CompanyId == companyId &&
                x.IsActive &&
                x.InteractionAt >= period.From &&
                x.InteractionAt <= period.To);
        if (saleEmployeeId.HasValue)
        {
            interactionQuery = interactionQuery.Where(x => x.AssignedSaleEmployeeId == saleEmployeeId);
        }

        var monthSourceRows = await interactionQuery
            .GroupBy(x => new { x.InteractionAt.Year, x.InteractionAt.Month })
            .Select(group => new
            {
                group.Key.Year,
                group.Key.Month,
                InteractionCount = group.Count(),
                LatestInteractionAt = group.Max(x => x.InteractionAt),
                LatestSourceChangedAt = group.Max(x => x.UpdatedDate ?? x.CreatedDate)
            })
            .OrderBy(x => x.Year)
            .ThenBy(x => x.Month)
            .ToListAsync(cancellationToken);
        var monthSources = monthSourceRows
            .Select(x => new MonthlyRollupDependencySource(
                x.Year,
                x.Month,
                x.InteractionCount,
                x.LatestInteractionAt,
                x.LatestSourceChangedAt))
            .ToList();
        if (monthSources.Count == 0)
        {
            return RollupSource.Ready([], 0, null, null);
        }

        var years = monthSources.Select(x => x.Year).Distinct().ToArray();
        var monthlySummaries = await _readDbContext.CustomerInteractionAiSummaries
            .AsNoTracking()
            .Where(x =>
                x.CustomerId == customerId &&
                x.CompanyId == companyId &&
                x.SaleEmployeeId == saleEmployeeId &&
                x.IsActive &&
                x.SummaryScope == CustomerInteractionSummaryScope.Monthly &&
                x.Year.HasValue &&
                years.Contains(x.Year.Value))
            .OrderByDescending(x => x.AiGeneratedDate ?? x.UpdatedDate ?? x.CreatedDate)
            .ToListAsync(cancellationToken);

        var items = new List<CustomerInteractionSummaryRollupItem>();
        var unavailableMonths = new List<string>();
        DateTime? latestSummaryChangedAt = null;
        foreach (var monthSource in monthSources)
        {
            var summary = monthlySummaries.FirstOrDefault(x =>
                x.Year == monthSource.Year &&
                x.Month == monthSource.Month);
            if (!IsDependencyReady(summary, monthSource.LatestSourceChangedAt))
            {
                unavailableMonths.Add($"{monthSource.Month:00}/{monthSource.Year}");
                continue;
            }

            items.Add(MapRollupItem($"Tháng {monthSource.Month:00}/{monthSource.Year}", summary!));
            latestSummaryChangedAt = Max(
                latestSummaryChangedAt,
                summary!.AiGeneratedDate ?? summary.UpdatedDate ?? summary.CreatedDate);
        }

        return unavailableMonths.Count > 0
            ? RollupSource.NotReady(
                $"Monthly summaries are not ready for months: {string.Join(", ", unavailableMonths)}.")
            : RollupSource.Ready(
                items,
                monthSources.Sum(x => x.InteractionCount),
                monthSources.Max(x => x.LatestInteractionAt),
                latestSummaryChangedAt);
    }

    private bool IsDependencyReady(
        CustomerInteractionAiSummary? summary,
        DateTime latestSourceChangedAt)
        => summary is
           {
               IsAiSuccess: true,
               PromptVersion: CurrentPromptVersion
           } &&
           summary.SourceModel == _client.Model &&
           summary.AiGeneratedDate.HasValue &&
           summary.AiGeneratedDate.Value >= latestSourceChangedAt;

    private static CustomerInteractionSummaryRollupItem MapRollupItem(
        string periodLabel,
        CustomerInteractionAiSummary summary)
        => new()
        {
            PeriodLabel = periodLabel,
            InteractionCount = summary.InteractionCount,
            Summary = summary.Summary,
            CustomerNeed = summary.CustomerNeed,
            CurrentStage = summary.CurrentStage,
            NextAction = summary.NextAction,
            Risk = summary.Risk,
            Sentiment = summary.Sentiment
        };

    private bool CanReuseRollup(
        CustomerInteractionAiSummary? existing,
        RollupSource source,
        bool forceRegenerate)
    {
        if (forceRegenerate ||
            existing is null ||
            (!existing.IsAiSuccess && !existing.IsAiSkipped) ||
            existing.InteractionCount != source.InteractionCount ||
            existing.PromptVersion != CurrentPromptVersion ||
            existing.SourceModel != _client.Model)
        {
            return false;
        }

        if (source.Items.Count == 0)
        {
            return existing.IsAiSkipped;
        }

        return existing.AiGeneratedDate.HasValue &&
               source.LatestSummaryChangedAt.HasValue &&
               existing.AiGeneratedDate.Value >= source.LatestSummaryChangedAt.Value;
    }

    private static DateTime Max(DateTime? left, DateTime right)
        => !left.HasValue || right > left.Value ? right : left.Value;

    private bool CanReuse(
        CustomerInteractionAiSummary? existing,
        InteractionPeriodStats? stats,
        DateTime? latestSourceChangedAt,
        bool forceRegenerate)
    {
        if (forceRegenerate ||
            existing is null ||
            (!existing.IsAiSuccess && !existing.IsAiSkipped) ||
            existing.PromptVersion != CurrentPromptVersion ||
            existing.SourceModel != _client.Model)
        {
            return false;
        }

        if (stats is null)
        {
            return existing.IsAiSkipped &&
                   (!latestSourceChangedAt.HasValue ||
                    existing.UpdatedDate.GetValueOrDefault(existing.CreatedDate) >= latestSourceChangedAt.Value);
        }

        return existing.AiGeneratedDate.HasValue &&
               (!latestSourceChangedAt.HasValue ||
                existing.AiGeneratedDate.Value >= latestSourceChangedAt.Value);
    }

    private async Task<CustomerInteractionAiSummaryDto> MapAsync(
        CustomerInteractionAiSummary entity,
        CancellationToken cancellationToken)
    {
        var customer = await _readDbContext.Customers
            .AsNoTracking()
            .Where(x => x.CustomerId == entity.CustomerId && x.CompanyId == entity.CompanyId)
            .Select(x => new { x.ExternalId, x.CustomerName })
            .FirstAsync(cancellationToken);
        var sale = entity.SaleEmployeeId.HasValue
            ? await _readDbContext.Employees
                .AsNoTracking()
                .Where(x =>
                    x.EmployeeId == entity.SaleEmployeeId &&
                    x.CompanyId == entity.CompanyId)
                .Select(x => new { x.ExternalId, x.FullName })
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        return new CustomerInteractionAiSummaryDto
        {
            Id = entity.Id,
            CustomerId = entity.CustomerId,
            CustomerCode = customer.ExternalId,
            CustomerName = customer.CustomerName,
            SaleEmployeeId = entity.SaleEmployeeId,
            SaleEmployeeCode = sale?.ExternalId,
            SaleEmployeeName = sale?.FullName,
            CompanyId = entity.CompanyId,
            SummaryScope = entity.SummaryScope,
            Year = entity.Year,
            Month = entity.Month,
            PeriodFrom = entity.PeriodFrom,
            PeriodTo = entity.PeriodTo,
            InteractionCount = entity.InteractionCount,
            PreviousSummary = entity.PreviousSummary,
            Summary = entity.Summary,
            CustomerNeed = entity.CustomerNeed,
            CurrentStage = entity.CurrentStage,
            NextAction = entity.NextAction,
            Risk = entity.Risk,
            Sentiment = entity.Sentiment,
            SourceModel = entity.SourceModel,
            PromptVersion = entity.PromptVersion,
            IsAiSuccess = entity.IsAiSuccess,
            IsAiSkipped = entity.IsAiSkipped,
            AiErrorMessage = entity.AiErrorMessage,
            AiGeneratedDate = entity.AiGeneratedDate,
            CreatedDate = entity.CreatedDate,
            UpdatedDate = entity.UpdatedDate,
            IsActive = entity.IsActive
        };
    }

    private static SummaryPeriod ResolvePeriod(
        GenerateCustomerInteractionAiSummaryRequest request,
        DateTime now)
        => request.SummaryScope switch
        {
            CustomerInteractionSummaryScope.Monthly => ResolveMonth(
                request.Year ?? now.Year,
                request.Month ?? now.Month),
            CustomerInteractionSummaryScope.Yearly => ResolveYear(request.Year ?? now.Year),
            CustomerInteractionSummaryScope.Lifetime => SummaryPeriod.Valid(LifetimeStart, now),
            CustomerInteractionSummaryScope.CustomRange
                when request.From.HasValue &&
                     request.To.HasValue &&
                     request.To.Value >= request.From.Value
                => SummaryPeriod.Valid(
                    request.From.Value,
                    request.To.Value.Date.AddDays(1).AddTicks(-1)),
            _ => SummaryPeriod.Invalid("Summary period is invalid.")
        };

    private static SummaryPeriod ResolveMonth(int year, int month)
        => year is >= 2000 and <= 2100 && month is >= 1 and <= 12
            ? SummaryPeriod.Valid(
                new DateTime(year, month, 1),
                new DateTime(year, month, 1).AddMonths(1).AddTicks(-1))
            : SummaryPeriod.Invalid("Summary month is invalid.");

    private static SummaryPeriod ResolveYear(int year)
        => year is >= 2000 and <= 2100
            ? SummaryPeriod.Valid(
                new DateTime(year, 1, 1),
                new DateTime(year + 1, 1, 1).AddTicks(-1))
            : SummaryPeriod.Invalid("Summary year is invalid.");

    private sealed record InteractionPeriodStats(
        int InteractionCount,
        DateTime LatestInteractionAt);

    private sealed record RollupDependencySource(
        int Key,
        int InteractionCount,
        DateTime LatestInteractionAt,
        DateTime LatestSourceChangedAt);

    private sealed record MonthlyRollupDependencySource(
        int Year,
        int Month,
        int InteractionCount,
        DateTime LatestInteractionAt,
        DateTime LatestSourceChangedAt);

    private sealed record RollupSource(
        bool IsReady,
        IReadOnlyList<CustomerInteractionSummaryRollupItem> Items,
        int InteractionCount,
        DateTime? LatestInteractionAt,
        DateTime? LatestSummaryChangedAt,
        string? Error)
    {
        public static RollupSource Ready(
            IReadOnlyList<CustomerInteractionSummaryRollupItem> items,
            int interactionCount,
            DateTime? latestInteractionAt,
            DateTime? latestSummaryChangedAt)
            => new(
                true,
                items,
                interactionCount,
                latestInteractionAt,
                latestSummaryChangedAt,
                null);

        public static RollupSource NotReady(string error)
            => new(false, [], 0, null, null, error);
    }

    private sealed record SummaryPeriod(DateTime From, DateTime To, bool IsValid, string? Error)
    {
        public static SummaryPeriod Valid(DateTime from, DateTime to) => new(from, to, true, null);
        public static SummaryPeriod Invalid(string error) => new(default, default, false, error);
    }

    private sealed record PendingMonthlySummary(
        CustomerInteractionAiSummaryGenerationBatchRequest Request,
        Customer Customer,
        SummaryPeriod Period,
        CustomerInteractionAiSummary Entity,
        bool IsNew,
        IReadOnlyList<CustomerInteractionPromptRow> Rows);
}

internal sealed record CustomerInteractionAiSummaryGenerationOutcome(
    OperationResult<CustomerInteractionAiSummaryDto> Result,
    bool AiRequested)
{
    public static CustomerInteractionAiSummaryGenerationOutcome Requested(
        OperationResult<CustomerInteractionAiSummaryDto> result)
        => new(result, true);

    public static CustomerInteractionAiSummaryGenerationOutcome NotRequested(
        OperationResult<CustomerInteractionAiSummaryDto> result)
        => new(result, false);
}

internal sealed record CustomerInteractionAiSummaryGenerationBatchRequest(
    Guid CustomerId,
    Guid CompanyId,
    Guid AuditEmployeeId,
    int Year,
    int Month);

internal sealed record CustomerInteractionAiSummaryGenerationBatchItemOutcome(
    CustomerInteractionAiSummaryGenerationBatchRequest Request,
    CustomerInteractionAiSummaryGenerationOutcome Outcome);

internal sealed record CustomerInteractionAiSummaryGenerationBatchOutcome(
    IReadOnlyList<CustomerInteractionAiSummaryGenerationBatchItemOutcome> Items,
    bool AiRequested)
{
    public static CustomerInteractionAiSummaryGenerationBatchOutcome Requested(
        IReadOnlyList<CustomerInteractionAiSummaryGenerationBatchItemOutcome> items)
        => new(items, true);

    public static CustomerInteractionAiSummaryGenerationBatchOutcome NotRequested(
        IReadOnlyList<CustomerInteractionAiSummaryGenerationBatchItemOutcome> items)
        => new(items, false);
}
