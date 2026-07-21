namespace HRM.Application.Features.PLM.SampleRequests.Dtos.Detail
{
    public sealed class SampleRequestDetailHeroDto
    {
        public Guid SampleRequestId { get; set; }
        public string ExternalId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? Title { get; set; }
        public string? ProductCode { get; set; }
        public string? ColourCode { get; set; }
        public string? ColourName { get; set; }
        public string? ColourNameLabel { get; set; }
        public string? CustomerExternalId { get; set; }
        public string? CustomerName { get; set; }
        public string? ManagerByName { get; set; }
        public string? BranchName { get; set; }
        public string? CompanyName { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? ExpectedDeliveryDate { get; set; }
        public bool IsActive { get; set; }
    }
}
