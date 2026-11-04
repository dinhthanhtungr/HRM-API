using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Queries.GetManufacturingFormulaChoices;

public sealed class ManufacturingFormulaChoiceDto
{
    public Guid FormulaId { get; init; }
    public string ExternalId { get; init; } = string.Empty;
    public string? OrderExternalId { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedDate { get; init; }
    public bool IsStandard { get; init; }
    public bool IsSelected { get; init; }
}
