using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Queries.GetQuotationCustomerTerms;

/// <summary>
/// Trả terms của báo giá gần nhất có terms active cho customer đang chọn, hoặc bộ mặc định nếu chưa có lịch sử.
/// </summary>
internal sealed class GetQuotationCustomerTermsQueryHandler
    : IRequestHandler<GetQuotationCustomerTermsQuery, OperationResult<QuotationCustomerTermsDto>>
{
    private readonly ICRMReadDbContext _dbContext;
    private readonly ICustomerVisibilityService _visibilityService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public GetQuotationCustomerTermsQueryHandler(
        ICRMReadDbContext dbContext,
        ICustomerVisibilityService visibilityService,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _visibilityService = visibilityService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<OperationResult<QuotationCustomerTermsDto>> Handle(
        GetQuotationCustomerTermsQuery request,
        CancellationToken cancellationToken)
    {
        if (request.CustomerId == Guid.Empty)
        {
            return OperationResult<QuotationCustomerTermsDto>.Fail("CustomerId is required.");
        }

        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        var customerIsVisible = await _visibilityService
            .ApplyCustomerVisibility(_dbContext.Customers.AsNoTracking(), scope)
            .AnyAsync(x => x.CustomerId == request.CustomerId, cancellationToken);
        if (!customerIsVisible)
        {
            return OperationResult<QuotationCustomerTermsDto>.Fail(
                "Customer was not found or is outside your visibility scope.");
        }

        var latestQuotation = await _visibilityService
            .ApplyQuotationVisibility(
                _dbContext.Quotations.AsNoTracking(),
                _dbContext.Customers.AsNoTracking(),
                scope)
            .Where(x =>
                x.CustomerId == request.CustomerId &&
                x.Terms.Any(term => term.IsActive))
            .OrderByDescending(x => x.SentDate ?? x.UpdatedDate ?? x.CreatedDate)
            .ThenByDescending(x => x.QuotationDate)
            .Select(x => new QuotationCustomerTermsDto
            {
                CustomerId = x.CustomerId,
                SourceQuotationId = x.QuotationId,
                SourceQuotationExternalId = x.ExternalId,
                SourceQuotationDate = x.QuotationDate,
                UsedDefaultTerms = false,
                Terms = x.Terms
                    .Where(term => term.IsActive)
                    .OrderBy(term => term.SortOrder)
                    .ThenBy(term => term.QuotationTermId)
                    .Select(term => new QuotationTermDto
                    {
                        QuotationTermId = term.QuotationTermId,
                        LabelVi = term.LabelVi,
                        LabelEn = term.LabelEn,
                        ValueVi = term.ValueVi,
                        ValueEn = term.ValueEn,
                        SortOrder = term.SortOrder,
                        IsActive = true
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (latestQuotation is not null)
        {
            return OperationResult<QuotationCustomerTermsDto>.Ok(latestQuotation);
        }

        return OperationResult<QuotationCustomerTermsDto>.Ok(new QuotationCustomerTermsDto
        {
            CustomerId = request.CustomerId,
            UsedDefaultTerms = true,
            Terms = QuotationTermDefaults.Create(_dateTimeProvider.Now)
        });
    }
}
