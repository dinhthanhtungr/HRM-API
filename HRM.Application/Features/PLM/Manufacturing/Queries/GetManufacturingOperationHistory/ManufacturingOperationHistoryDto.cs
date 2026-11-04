using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Domain.Enums.Manufacturings;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Queries.GetManufacturingOperationHistory;

public sealed record ManufacturingOperationHistoryDto(
    DateTime CreatedAt,
    string? ProductCode,
    string? ExternalId,
    string? MachineId,
    int? ScrewSpeed,
    int? ScrewCurrent,
    int? FeederSpeed,
    IReadOnlyList<ManufacturingTemperatureZoneDto> Zones
);
