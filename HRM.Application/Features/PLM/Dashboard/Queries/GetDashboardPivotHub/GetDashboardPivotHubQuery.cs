using HRM.Application.Features.PLM.Dashboard.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.Dashboard.Queries.GetDashboardPivotHub;

public sealed class GetDashboardPivotHubQuery : IRequest<PlmDashboardPivotHubDto>
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public Guid? CompanyId { get; set; }
    public Guid? ProductId { get; set; }
    public Guid? CustomerId { get; set; }
    public string? Status { get; set; }
}
