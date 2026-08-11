using HRM.Application.Abstractions.Documents;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.CRM.Quotations.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Queries.ExportQuotationPdf;

internal sealed class ExportQuotationPdfQueryHandler
    : IRequestHandler<ExportQuotationPdfQuery, OperationResult<QuotationPdfFileDto>>
{
    private readonly ICRMReadDbContext _dbContext;
    private readonly ICustomerVisibilityService _visibilityService;
    private readonly IQuotationPdfRenderer _renderer;

    public ExportQuotationPdfQueryHandler(
        ICRMReadDbContext dbContext,
        ICustomerVisibilityService visibilityService,
        IQuotationPdfRenderer renderer)
    {
        _dbContext = dbContext;
        _visibilityService = visibilityService;
        _renderer = renderer;
    }

    public async Task<OperationResult<QuotationPdfFileDto>> Handle(
        ExportQuotationPdfQuery request,
        CancellationToken cancellationToken)
    {
        if (request.QuotationId == Guid.Empty)
        {
            return OperationResult<QuotationPdfFileDto>.Fail("QuotationId is invalid.");
        }

        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        var quotation = await _visibilityService
            .ApplyQuotationVisibility(
                _dbContext.Quotations.AsNoTracking(),
                _dbContext.Customers.AsNoTracking(),
                scope)
            .Where(x => x.QuotationId == request.QuotationId)
            .Select(x => new QuotationPdfDocumentDto
            {
                ExternalId = x.ExternalId,
                QuotationDate = x.QuotationDate,
                ValidUntil = x.ValidUntil,
                Currency = x.Currency,
                CompanyName = x.Company.Name,
                CompanyAddress = x.Company.Address,
                CompanyPhone = x.Company.Phone,
                CompanyEmail = x.Company.Email,
                CustomerName = x.Customer.CustomerName,
                CustomerAddress = x.Customer.RegistrationAddress,
                CustomerPhone = x.Customer.Phone,
                CustomerFax = x.Customer.FaxNumber,
                ContactName = x.ContactName,
                ContactPhone = x.Contact != null ? x.Contact.Phone : null,
                ContactEmail = x.Contact != null ? x.Contact.Email : null,
                SaleEmployeeName = x.SaleEmployee.FullName,
                SaleEmployeePhone = x.SaleEmployee.PhoneNumber,
                SaleEmployeeEmail = x.SaleEmployee.Email,
                SubTotal = x.SubTotal,
                DiscountAmount = x.DiscountAmount,
                TaxPercent = x.TaxPercent,
                TaxAmount = x.TaxAmount,
                TotalAmount = x.TotalAmount,
                PaymentTerms = x.PaymentTerms,
                DeliveryTerms = x.DeliveryTerms,
                Note = x.Note,
                Lines = x.Lines
                    .OrderBy(line => line.SortOrder)
                    .ThenBy(line => line.QuotationLineId)
                    .Select(line => new QuotationPdfLineDto
                    {
                        ProductCode = line.ProductExternalIdSnapshot,
                        ProductName = line.ProductNameSnapshot,
                        Quantity = line.Quantity,
                        Unit = line.Unit,
                        PriceMode = line.PriceMode,
                        UnitPrice = line.UnitPrice,
                        DiscountPercent = line.DiscountPercent,
                        LineTotal = line.LineTotal,
                        Note = line.Note,
                        SortOrder = line.SortOrder,
                        PriceTiers = line.PriceTiers
                            .OrderBy(tier => tier.SortOrder)
                            .ThenBy(tier => tier.QuotationLinePriceTierId)
                            .Select(tier => new QuotationPdfPriceTierDto
                            {
                                QuantityRangeLabel = tier.QuantityRangeLabel,
                                UnitPrice = tier.UnitPrice,
                                SortOrder = tier.SortOrder
                            })
                            .ToList()
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (quotation is null)
        {
            return OperationResult<QuotationPdfFileDto>.Fail(
                "Quotation was not found or is outside your visibility scope.");
        }

        var content = _renderer.Render(quotation);
        return OperationResult<QuotationPdfFileDto>.Ok(new QuotationPdfFileDto
        {
            FileName = $"Bao-gia-{SanitizeFileName(quotation.ExternalId)}.pdf",
            Content = content
        });
    }

    private static string SanitizeFileName(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var sanitized = new string(value
            .Select(character => invalidCharacters.Contains(character) ? '-' : character)
            .ToArray());

        return string.IsNullOrWhiteSpace(sanitized) ? "quotation" : sanitized;
    }
}
