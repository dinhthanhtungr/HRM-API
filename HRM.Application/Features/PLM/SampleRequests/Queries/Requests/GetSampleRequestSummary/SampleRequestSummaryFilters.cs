using System.ComponentModel.DataAnnotations;
using HRM.Application.Commons.Pagination;
using HRM.Domain.Enums.SampleRequests;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSummary;

public class SampleRequestSummaryFilters : PaginationQuery, IValidatableObject
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public Guid? CompanyId { get; set; }
    public Guid? ProductId { get; set; }
    public string? Color { get; set; }
    public string? AdditiveCode { get; set; }
    public SampleRequestStatus? Status { get; set; }
    [MaxLength(20)] public Guid[] EmployeeIds { get; set; } = [];
    [MaxLength(20)] public Guid[] GroupIds { get; set; } = [];
    [MaxLength(20)] public Guid[] FormulaIds { get; set; } = [];
    [MaxLength(20)] public string[] ColourCodes { get; set; } = [];
    [MaxLength(20)] public string[] EndUsers { get; set; } = [];
    [MaxLength(20)] public Guid[] CustomerIds { get; set; } = [];
    [MaxLength(20)] public Guid[] CustomerCodeIds { get; set; } = [];
    [MaxLength(20)] public Guid[] CustomerTaxNumberIds { get; set; } = [];
    [MaxLength(20)] public Guid[] ProductIds { get; set; } = [];
    [MaxLength(20)] public Guid[] SampleRequestIds { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var ids = EmployeeIds.Concat(GroupIds).Concat(FormulaIds).Concat(CustomerIds)
            .Concat(CustomerCodeIds).Concat(CustomerTaxNumberIds).Concat(ProductIds).Concat(SampleRequestIds).ToArray();
        if (ids.Contains(Guid.Empty))
            yield return new ValidationResult("Filter IDs must not be empty.");
        if (ids.Length + ColourCodes.Length + EndUsers.Length > 20)
            yield return new ValidationResult("Select no more than 20 search filters.");
        if (ColourCodes.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 100))
            yield return new ValidationResult("Colour codes must contain 1 to 100 characters.", [nameof(ColourCodes)]);
        if (EndUsers.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 100))
            yield return new ValidationResult("End users must contain 1 to 100 characters.", [nameof(EndUsers)]);
        if (FromDate.HasValue && ToDate.HasValue && FromDate.Value.Date > ToDate.Value.Date)
            yield return new ValidationResult("The end date must not precede the start date.", [nameof(ToDate)]);
        if (ToDate?.Date == DateTime.MaxValue.Date)
            yield return new ValidationResult("The end date is outside the supported range.", [nameof(ToDate)]);
    }
}
