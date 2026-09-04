namespace HRM.Application.Features.CRM.Quotations.Dtos;

/// <summary>
/// Terms hiển thị cho form tạo báo giá sau khi Sale chọn khách hàng. Các terms chỉ là gợi ý;
/// FE phải gửi lại chúng khi tạo báo giá để tạo snapshot riêng cho quotation mới.
/// </summary>
public sealed class QuotationCustomerTermsDto
{
    public Guid CustomerId { get; init; }
    public Guid? SourceQuotationId { get; init; }
    public string? SourceQuotationExternalId { get; init; }
    public DateTime? SourceQuotationDate { get; init; }
    public bool UsedDefaultTerms { get; init; }
    public IReadOnlyList<QuotationTermDto> Terms { get; init; } = [];
}
