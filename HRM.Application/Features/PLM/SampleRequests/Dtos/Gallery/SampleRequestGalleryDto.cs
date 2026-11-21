namespace HRM.Application.Features.PLM.SampleRequests.Dtos.Gallery;

public sealed class SampleRequestGalleryDto
{
    public Guid SampleRequestId { get; set; }
    public string? ExternalId { get; set; }
    public string? ProductName { get; set; }
    public string? ColourCode { get; set; }
    public string? ColorName { get; set; }
    public string? CategoryName { get; set; }
    public string? AdditiveName { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerExternalId { get; set; }
    public string? Status { get; set; }
    public DateTime? SampleRequestCreatedDate { get; set; }
    public int ImageCount { get; set; }
    public SampleRequestGalleryImageDto? CoverImage { get; set; }
}

public sealed class SampleRequestGalleryImageDto
{
    public Guid AttachmentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
}
