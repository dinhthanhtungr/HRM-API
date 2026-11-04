using HRM.Application.Commons.Models;

namespace HRM.Application.Features.CRM.Quotations.Dtos;

public sealed class QuotationCreateConflictFailure : OperationResult<QuotationCreateResultDto>
{
    public string Code => "quotation.create_conflict";
    public Guid? ExistingQuotationId { get; init; }
}
