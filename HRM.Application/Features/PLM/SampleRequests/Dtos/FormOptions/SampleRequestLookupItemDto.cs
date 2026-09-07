namespace HRM.Application.Features.PLM.SampleRequests.Dtos.FormOptions;

public sealed class SampleRequestLookupItemDto
{
    public Guid SampleRequestId { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public string? CustomerExternalId { get; set; }
    public string? CustomerName { get; set; }
    public Guid ProductId { get; set; }
    public string? ProductCode { get; set; }
    public string? ProductName { get; set; }
    /// <summary>
    /// Formula có thể dùng khi lập Sale Order: Formula được chọn trên Sample Request,
    /// hoặc Formula của Trial active mới nhất nếu request đang ở lifecycle gửi mẫu.
    /// </summary>
    public Guid? FormulaId { get; set; }
    public string? FormulaExternalId { get; set; }
    public DateTime CreatedDate { get; set; }
}
