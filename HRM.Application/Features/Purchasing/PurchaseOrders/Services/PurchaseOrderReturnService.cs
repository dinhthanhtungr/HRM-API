using HRM.Application.Abstractions.Commons.ExternalIds;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.Purchasing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.Purchasing.PurchaseOrders.Dtos;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Entities.WarehouseSchema;
using HRM.Domain.Enums.Devandqa;
using HRM.Domain.Enums.WareHouses;
using Microsoft.EntityFrameworkCore;
using Shared.Enums;
using StockType = HRM.Domain.Enums.WareHouses.StockType;
using HRM.Domain.Enums.Orders;

namespace HRM.Application.Features.Purchasing.PurchaseOrders.Services;

/// <summary>Xuất trả chỉ tạo yêu cầu kho; không ghi ledger/tồn, không thay quyết định QC hoặc PO.</summary>
internal sealed class PurchaseOrderReturnService(IPurchaseOrderDbContext db, ICurrentUser user,
    ICurrentUserPermissionService permissions, IExternalIdService ids, IDateTimeProvider clock)
{
    private const string RequestTitle = "PHIẾU YÊU CẦU XUẤT HÀNG TRẢ NCC";
    private const string ReturnDocumentName = PurchaseReturnReadModel.DocumentName;

    public async Task<OperationResult<PurchaseReturnOptionsDto>> ReadAsync(Guid id, CancellationToken ct, bool includeAccepted = false)
    {
        var order = await FindOrderAsync(id, ct);
        if (order is null) return OperationResult<PurchaseReturnOptionsDto>.Fail("Không tìm thấy đơn trả hàng hoặc không có quyền truy cập.");
        var options = await LoadAsync(order, ct, includeAccepted);
        return options is null
            ? OperationResult<PurchaseReturnOptionsDto>.Fail("Mã ĐĐH bị trống hoặc trùng; không xác định được nguồn nhập kho.")
            : OperationResult<PurchaseReturnOptionsDto>.Ok(options);
    }

    public async Task<OperationResult<PurchaseReturnRequestDto>> CreateAsync(Guid id, CreatePurchaseReturnRequest input, CancellationToken ct)
    {
        if (!CanAccess()) return OperationResult<PurchaseReturnRequestDto>.Fail("Bạn không có quyền tạo yêu cầu xuất trả.");
        if (input.ClientRequestId == Guid.Empty)
            return OperationResult<PurchaseReturnRequestDto>.Fail("Mã thao tác không hợp lệ.");
        var reason = input.Reason?.Trim();
        var content = input.Content?.Trim();
        if (string.IsNullOrEmpty(reason) || reason.Length > 2000 || content?.Length > 4000)
            return OperationResult<PurchaseReturnRequestDto>.Fail("Nhập lý do trả hàng tối đa 2000 ký tự; nội dung tối đa 4000 ký tự.");
        if (input.Lines is not { Count: > 0 and <= 100 } ||
            input.Lines.Any(x => x is null || x.VoucherDetailId <= 0 || x.QuantityKg <= 0 || decimal.Round(x.QuantityKg, 2) != x.QuantityKg) ||
            input.Lines.Select(x => x.VoucherDetailId).Distinct().Count() != input.Lines.Count)
            return OperationResult<PurchaseReturnRequestDto>.Fail("Chọn từ 1 đến 100 dòng, số kg phải lớn hơn 0 và tối đa 2 số lẻ; không lặp dòng.");
        if (user.EmployeeId is not { } employee || employee == Guid.Empty ||
            !await db.Employees.AnyAsync(x => x.EmployeeId == employee && x.CompanyId == user.CompanyId && x.IsActive, ct))
            return OperationResult<PurchaseReturnRequestDto>.Fail("Không xác định được nhân viên thuộc công ty hiện tại.");
        await using var transaction = await db.BeginTransactionAsync(ct);
        // PostgreSQL row lock serializes concurrent return requests for this PO across API instances.
        await db.LockPurchaseOrderAsync(id, user.CompanyId!.Value, ct);
        var order = await FindOrderAsync(id, ct);
        if (order is null) return OperationResult<PurchaseReturnRequestDto>.Fail("Không tìm thấy đơn trả hàng hoặc không có quyền truy cập.");
        var options = await LoadAsync(order, ct, input.IncludeAccepted);
        if (options is null) return OperationResult<PurchaseReturnRequestDto>.Fail("Không xác định duy nhất nguồn nhập kho của ĐĐH.");
        if (input.ClientRequestId is { } operationId)
        {
            var previous = await db.PurchaseOrderDocuments.AsNoTracking().FirstOrDefaultAsync(x => x.PurchaseOrderDocumentId == operationId, ct);
            if (previous is not null)
            {
                if (previous.PurchaseOrderId != id || previous.CreatedBy != employee || previous.DocumentName != ReturnDocumentName)
                    return OperationResult<PurchaseReturnRequestDto>.Fail("Mã thao tác đã được sử dụng. Tải lại dữ liệu.");
                var saved = options.Requests.SingleOrDefault(x => x.RequestCode == previous.DocumentCode);
                return saved is null ? OperationResult<PurchaseReturnRequestDto>.Fail("Không tìm thấy yêu cầu đã lưu của thao tác này.")
                    : OperationResult<PurchaseReturnRequestDto>.Ok(saved);
            }
        }
        else if (options.HasOpenRequest)
            return OperationResult<PurchaseReturnRequestDto>.Fail("Đã có yêu cầu đang xử lý. Cập nhật giao diện để lập thêm với mã thao tác chống trùng.");
        var selected = new List<(PurchaseReturnLineDto Source, decimal Kg)>();
        foreach (var line in input.Lines)
        {
            var source = options.Lines.SingleOrDefault(x => x.VoucherDetailId == line.VoucherDetailId);
            if (source is null || line.QuantityKg > source.RemainingQuantityKg)
                return OperationResult<PurchaseReturnRequestDto>.Fail("Dòng nhập/QC không còn hợp lệ hoặc số kg vượt lượng còn được yêu cầu trả. Tải lại dữ liệu.");
            selected.Add((source, line.QuantityKg));
        }
        var request = new WarehouseRequest
        {
            RequestCode = await ids.GenerateMonthlyCodeAsync(user.CompanyId!.Value, DocumentPrefix.PRQ.ToString(), ct),
            RequestName = RequestTitle,
            ReqStatus = WarehouseRequestStatus.Pending, ReqType = WareHouseRequestType.ExportReturn,
            codeFromRequest = order.ExternalId!, CompanyId = user.CompanyId!.Value,
            CreatedBy = employee, CreatedDate = clock.Now, UpdatedBy = employee, UpdatedDate = clock.Now,
            WarehouseRequestDetails = selected.Select(x => new WarehouseRequestDetail
            {
                ProductCode = x.Source.MaterialCode, ProductName = x.Source.MaterialName,
                LotNumber = x.Source.LotNumber, WeightKg = x.Kg, UnitName = "Kg",
                ItemStockType = (StockType)x.Source.StockType, StockStatus = ((StockType)x.Source.StockType).ToString(),
            }).ToList(),
        };
        db.WarehouseRequests.Add(request);
        // Nội dung nghiệp vụ thuộc chứng từ PO; phiếu kho chỉ mang tên chuẩn và mã nguồn ĐĐH.
        var note = $"Lý do: {reason}" + (string.IsNullOrEmpty(content) ? "" : $"\nNội dung: {content}");
        db.PurchaseOrderDocuments.Add(new PurchaseOrderDocument
        {
            PurchaseOrderDocumentId = input.ClientRequestId ?? Guid.NewGuid(), PurchaseOrderId = order.PurchaseOrderId,
            DocumentType = PurchaseOrderDocumentType.Other, DocumentCode = request.RequestCode,
            DocumentName = ReturnDocumentName, Note = note,
            CreatedBy = employee, CreatedDate = clock.Now, UpdatedBy = employee, UpdatedDate = clock.Now,
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return OperationResult<PurchaseReturnRequestDto>.Ok(ToDto(request) with { ReturnNote = note,
            RequestedQuantityKg = selected.Sum(x => x.Kg), OutstandingQuantityKg = selected.Sum(x => x.Kg) });
    }

    private bool CanAccess() => user.IsAuthenticated && PurchaseOrderAccess.CanManage(permissions) &&
        user.CompanyId is { } company && company != Guid.Empty;

    private Task<PurchaseOrder?> FindOrderAsync(Guid id, CancellationToken ct) => !CanAccess()
        ? Task.FromResult<PurchaseOrder?>(null)
        : db.PurchaseOrders.AsNoTracking().FirstOrDefaultAsync(x => x.PurchaseOrderId == id &&
            x.CompanyId == user.CompanyId && x.IsActive == true &&
            (x.Status == PurchaseOrderStatus.ExchangeRequested || x.Status == PurchaseOrderStatus.InProgress || x.Status == PurchaseOrderStatus.Completed), ct);

    private async Task<PurchaseReturnOptionsDto?> LoadAsync(PurchaseOrder order, CancellationToken ct, bool includeAccepted)
    {
        var code = PurchaseReturnReadModel.Normalize(order.ExternalId);
        var all = await new PurchaseReturnReadModel(db).ReadAsync(order.CompanyId!.Value, [code], ct);
        if (!all.TryGetValue(code, out var facts)) return null;
        var reserved = facts.Committed;
        var requested = reserved.GroupBy(x => Key(x.Code, x.Lot)).ToDictionary(x => x.Key, x => x.Sum(d => d.Kg));
        var lines = new List<PurchaseReturnLineDto>();
        foreach (var lot in facts.Sources.GroupBy(x => Key(x.Code, x.Lot)))
        {
            var groups = lot.GroupBy(x => x.Type)
                .OrderByDescending(x => x.Key).ToList();
            var ownRequested = groups.ToDictionary(x => x.Key, group => Math.Min(group.Sum(x => x.Kg),
                reserved.Where(x => Key(x.Code, x.Lot) == lot.Key && x.Type == group.Key).Sum(x => x.Kg)));
            // Legacy untyped requests or a later QC reclassification must still consume this lot's cap.
            var unallocated = Math.Max(0, requested.GetValueOrDefault(lot.Key) - ownRequested.Values.Sum());
            foreach (var group in groups)
            {
                var first = group.OrderBy(x => x.Id).First();
                var received = group.Sum(x => x.Kg);
                var extra = Math.Min(Math.Max(0, received - ownRequested[group.Key]), unallocated);
                unallocated -= extra;
                var used = ownRequested[group.Key] + extra;
                if (group.Key == StockType.RawMaterial && !includeAccepted) continue;
                lines.Add(new PurchaseReturnLineDto(first.Id, first.Code, first.Name,
                    first.Lot, group.Key == StockType.DefectiveRawMaterial ? received : 0,
                    used, Math.Max(0, received - used), (int)group.Key, received));
            }
        }
        lines = lines.OrderBy(x => x.MaterialCode).ThenBy(x => x.LotNumber).ThenBy(x => x.StockType).ToList();
        return new(lines, facts.Requests, facts.Requests.Any(x => x.IsActive && x.Status is (int)WarehouseRequestStatus.Pending or (int)WarehouseRequestStatus.Approved), facts.Progress);
    }

    private static string NormalizeCode(string? code) => (code ?? "").Trim().ToUpperInvariant();
    private static (string Code, string Lot) Key(string? code, string? lot) => (NormalizeCode(code), lot ?? "");
    private static PurchaseReturnRequestDto ToDto(WarehouseRequest request) =>
        new(request.RequestId, request.RequestCode, (int)request.ReqStatus, request.CreatedDate);
}
