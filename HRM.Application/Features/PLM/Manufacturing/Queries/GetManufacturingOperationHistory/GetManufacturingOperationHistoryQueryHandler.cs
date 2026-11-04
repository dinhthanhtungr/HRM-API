using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.PLM.Manufacturing.Services.Access;
using HRM.Application.Features.PLM.Manufacturing.Services.Formulas;
using HRM.Domain.Enums.Manufacturings;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Manufacturing.Queries.GetManufacturingOperationHistory;

internal sealed class GetManufacturingOperationHistoryQueryHandler(
    IManufacturingDbContext db,
    ICurrentUser user,
    ICurrentUserPermissionService permissions
) : IRequestHandler<GetManufacturingOperationHistoryQuery, OperationResult<ManufacturingOperationHistoryResult>>
{
    public async Task<OperationResult<ManufacturingOperationHistoryResult>> Handle(GetManufacturingOperationHistoryQuery request, CancellationToken ct)
    {
        var error = ManufacturingAccess.Validate(user, permissions, ApplicationPermissions.PLM.ViewProductionOrders);
        if (error is not null)
        {
            return Fail(error);
        }

        var skip = ((long)request.PageNumber - 1) * request.PageSize;
        if (request.OrderId == Guid.Empty || request.SourceId == Guid.Empty || request.PageNumber < 1
            || request.PageSize is < 1 or > 500
            || skip > int.MaxValue)
        {
            return Fail("Bộ lọc lịch sử máy không hợp lệ.");
        }

        var company = user.CompanyId!.Value;
        var order = await db.MfgProductionOrders.AsNoTracking().FirstOrDefaultAsync(x => x.MfgProductionOrderId == request.OrderId && x.CompanyId == company && x.IsActive, ct);
        if (order is null)
        {
            return Fail("Không tìm thấy lệnh sản xuất trong công ty.");
        }

        var option = (await ManufacturingFormulaOptionReader.ReadAsync(db, order, ct)).FirstOrDefault(x => x.FormulaId == request.SourceId);
        string? code = option?.ExternalId;
        if (option is null
            && await db.ProductionSelectVersions.AsNoTracking().AnyAsync(x => x.CompanyId == company && x.MfgProductionOrderId == request.OrderId
            && x.ManufacturingFormulaId == request.SourceId, ct))
        {
            code = await db.ManufacturingFormulas.AsNoTracking().Where(x => x.CompanyId == company && x.IsActive && x.ManufacturingFormulaId == request.SourceId)
                .Select(x => x.ExternalId)
                .FirstOrDefaultAsync(ct);
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            return Fail("Nguồn lịch sử không thuộc lệnh sản xuất.");
        } // The legacy keyless history table has no CompanyId. Never infer ownership from

        // a code shared with another company, including inactive source documents.
        if (await db.ManufacturingFormulas.AsNoTracking().AnyAsync(x => x.CompanyId != company && x.ExternalId == code, ct)
            || await db.Formulas.AsNoTracking().AnyAsync(x => x.CompanyId != company && x.ExternalId == code, ct))
        {
            return Fail("Không thể xác minh company của mã lịch sử máy này.");
        }

        var query = db.OperationHistoryMds.AsNoTracking().Where(x => x.ExternalId == code);
        var count = await query.CountAsync(ct);
        var latest = count == 0 ? null : await query.MaxAsync(x => (DateTime? )x.CreatedAt, ct);
        var rows = await query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.MachineId).Skip((int)skip).Take(request.PageSize)
            .Select(x => new ManufacturingOperationHistoryDto(
            x.CreatedAt,
            x.ProductCode,
            x.ExternalId,
            x.MachineId,
            x.ScrewSpeed,
            x.ScrewCurrent,
            x.FeederSpeed,
            new ManufacturingTemperatureZoneDto[]
            {
                new(x.Set1, x.Act1),
                new(x.Set2, x.Act2),
                new(x.Set3, x.Act3),
                new(x.Set4, x.Act4),
                new(x.Set5, x.Act5),
                new(x.Set6, x.Act6),
                new(x.Set7, x.Act7),
                new(x.Set8, x.Act8),
                new(x.Set9, x.Act9),
                new(x.Set10, x.Act10),
                new(x.Set11, x.Act11),
                new(x.Set12, x.Act12),
                new(x.Set13, x.Act13)
            }
        ))
            .ToListAsync(ct);
        return OperationResult<ManufacturingOperationHistoryResult>.Ok(new ManufacturingOperationHistoryResult(
            new PagedResult<ManufacturingOperationHistoryDto>(rows, count, request.PageNumber, request.PageSize),
            count > 0,
            latest
        ));
    }

    private static OperationResult<ManufacturingOperationHistoryResult> Fail(string message) => OperationResult<ManufacturingOperationHistoryResult>.Fail(message);
}
