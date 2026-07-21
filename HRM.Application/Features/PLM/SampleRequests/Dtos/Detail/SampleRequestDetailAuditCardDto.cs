namespace HRM.Application.Features.PLM.SampleRequests.Dtos.Detail
{
    public sealed class SampleRequestDetailAuditCardDto
    {
        public DateTime CreatedDate { get; set; }
        public Guid CreatedBy { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public Guid? UpdatedBy { get; set; }
        public string? UpdatedByName { get; set; }
        public DateTime? ProductCreatedDate { get; set; }
        public Guid? ProductCreatedBy { get; set; }
        public string? ProductCreatedByName { get; set; }
        public Guid? SendBy { get; set; }
        public string? SendByName { get; set; }
        public DateTime? SendDate { get; set; }
    }
}
