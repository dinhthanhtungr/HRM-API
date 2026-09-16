using HRM.Application.Abstractions.Commons.ExternalIds;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.Purchasing;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.Purchasing.PurchaseOrders.Dtos;
using HRM.Application.Features.Purchasing.PurchaseOrders.Services;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Entities.OrderSchema;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Enums;

namespace HRM.Application.Features.Purchasing.PurchaseOrders.Commands.CreatePurchaseOrder;

/// <summary>Tạo PO và đồng bộ giá NCC trong một transaction; các field server-owned được resolve tại backend.</summary>
internal sealed class CreatePurchaseOrderCommandHandler
    : IRequestHandler<CreatePurchaseOrderCommand, OperationResult<PurchaseOrderDetailDto>>
{
    private readonly IPurchaseOrderDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentUserPermissionService _permissionService;
    private readonly IExternalIdService _externalIdService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public CreatePurchaseOrderCommandHandler(
        IPurchaseOrderDbContext dbContext,
        ICurrentUser currentUser,
        ICurrentUserPermissionService permissionService,
        IExternalIdService externalIdService,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _permissionService = permissionService;
        _externalIdService = externalIdService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<OperationResult<PurchaseOrderDetailDto>> Handle(
        CreatePurchaseOrderCommand command,
        CancellationToken cancellationToken)
    {
        if (!PurchaseOrderAccess.CanManage(_permissionService))
            return OperationResult<PurchaseOrderDetailDto>.Fail("Bạn không có quyền tạo đơn mua hàng.");

        var validationError = Validate(command.Request);
        if (validationError is not null)
            return OperationResult<PurchaseOrderDetailDto>.Fail(validationError);

        var companyId = PurchaseOrderAccess.RequireCompanyId(_currentUser);
        var employeeId = PurchaseOrderAccess.RequireEmployeeId(_currentUser);
        var request = command.Request;

        var supplier = await LoadSupplierAsync(request.SupplierId, companyId, cancellationToken);
        if (supplier is null)
            return OperationResult<PurchaseOrderDetailDto>.Fail("Nhà cung cấp không tồn tại hoặc không thuộc công ty hiện tại.");

        var materialIds = request.Items.Select(x => x.MaterialId).Distinct().ToArray();
        var materials = await _dbContext.Materials.AsNoTracking()
            .Where(x => materialIds.Contains(x.MaterialId) && x.CompanyId == companyId && x.IsActive == true)
            .Select(x => new MaterialSnapshot(x.MaterialId, x.ExternalId, x.Name, x.Package))
            .ToDictionaryAsync(x => x.MaterialId, cancellationToken);
        if (materials.Count != materialIds.Length)
            return OperationResult<PurchaseOrderDetailDto>.Fail("Một hoặc nhiều vật tư không tồn tại hoặc không thuộc công ty hiện tại.");

        var merchandiseOrderIds = request.MerchandiseOrderIds.Where(x => x != Guid.Empty).Distinct().ToArray();
        var merchandiseOrders = await _dbContext.MerchandiseOrders.AsNoTracking()
            .Where(x => merchandiseOrderIds.Contains(x.MerchandiseOrderId) && x.CompanyId == companyId && x.IsActive)
            .Select(x => new MerchandiseOrderSnapshot(x.MerchandiseOrderId, x.ExternalId))
            .ToListAsync(cancellationToken);
        if (merchandiseOrders.Count != merchandiseOrderIds.Length)
            return OperationResult<PurchaseOrderDetailDto>.Fail("Một hoặc nhiều SaleOrder không tồn tại hoặc không thuộc công ty hiện tại.");

        var employee = await _dbContext.Employees.AsNoTracking()
            .Where(x => x.EmployeeId == employeeId)
            .Select(x => new EmployeeSnapshot(x.ExternalId, x.FullName, x.PhoneNumber, x.Email))
            .FirstOrDefaultAsync(cancellationToken);
        var supplierPrices = await _dbContext.MaterialsSuppliers
            .Where(x => x.SupplierId == supplier.SupplierId && materialIds.Contains(x.MaterialId))
            .ToDictionaryAsync(x => x.MaterialId, cancellationToken);

        await using var transaction = await _dbContext.BeginTransactionAsync(cancellationToken);
        var now = _dateTimeProvider.Now;
        var purchaseOrderId = Guid.CreateVersion7();
        var subtotal = request.Items.Sum(x => x.Quantity * x.UnitPriceAgreed);
        var totalPrice = subtotal + decimal.Round(subtotal * (request.Vat ?? 0) / 100m, 6, MidpointRounding.AwayFromZero);
        var snapshot = CreateSnapshot(request, supplier, employee, totalPrice);
        var purchaseOrder = new PurchaseOrder
        {
            PurchaseOrderId = purchaseOrderId,
            ExternalId = await _externalIdService.GenerateMonthlyCodeAsync(companyId, DocumentPrefix.DDH.ToString(), cancellationToken),
            OrderType = Normalize(request.OrderType), SupplierId = supplier.SupplierId,
            Comment = Normalize(request.Comment), PLPUComment = Normalize(request.PlpuComment),
            RequestDeliveryDate = request.RequestDeliveryDate, Status = PurchaseOrderStatus.Pending,
            CompanyId = companyId, CreateDate = now, CreatedBy = employeeId,
            UpdatedDate = now, UpdatedBy = employeeId,
            PurchaseOrderSnapshotId = snapshot.PurchaseOrderSnapshotId, IsActive = true
        };

        var details = BuildDetails(request.Items, materials, supplierPrices, purchaseOrderId);
        await _dbContext.PurchaseOrderSnapshots.AddAsync(snapshot, cancellationToken);
        await _dbContext.PurchaseOrders.AddAsync(purchaseOrder, cancellationToken);
        await _dbContext.PurchaseOrderDetails.AddRangeAsync(details, cancellationToken);
        await _dbContext.PurchaseOrderLinks.AddRangeAsync(merchandiseOrderIds.Select(id => new PurchaseOrderLink
        {
            PurchaseOrderLinkId = Guid.CreateVersion7(), PurchaseOrderId = purchaseOrderId,
            MerchandiseOrderId = id, IsActive = true
        }), cancellationToken);
        await SyncSupplierPricesAsync(request.Items, supplierPrices, supplier.SupplierId, employeeId, now, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var dto = PurchaseOrderDtoMapper.Map(
            purchaseOrder, supplier.SupplierName, snapshot,
            details.Select(PurchaseOrderDtoMapper.MapLine).ToList(),
            string.Join(", ", merchandiseOrders.Select(x => x.ExternalId)));
        return OperationResult<PurchaseOrderDetailDto>.Ok(dto, "Tạo đơn mua hàng thành công.");
    }

    private async Task<SupplierSnapshot?> LoadSupplierAsync(Guid supplierId, Guid companyId, CancellationToken ct) =>
        await _dbContext.Suppliers.AsNoTracking()
            .Where(x => x.SupplierId == supplierId && x.CompanyId == companyId && x.IsActive == true)
            .Select(x => new SupplierSnapshot(
                x.SupplierId, x.ExternalId, x.SupplierName, x.Phone, x.RegistrationAddress,
                x.SupplierContacts.Where(c => c.IsActive == true && c.IsPrimary == true)
                    .Select(c => ((c.FirstName ?? string.Empty) + " " + (c.LastName ?? string.Empty)).Trim()).FirstOrDefault(),
                x.SupplierAddresses.Where(a => a.IsActive == true && a.IsPrimary == true)
                    .Select(a => a.AddressLine).FirstOrDefault()))
            .FirstOrDefaultAsync(ct);

    private static List<PurchaseOrderDetail> BuildDetails(
        IEnumerable<PurchaseOrderLineRequest> items,
        IReadOnlyDictionary<Guid, MaterialSnapshot> materials,
        IReadOnlyDictionary<Guid, MaterialsSupplier> supplierPrices,
        Guid purchaseOrderId)
    {
        var lineNo = 1;
        return items.Select(item =>
        {
            var material = materials[item.MaterialId];
            supplierPrices.TryGetValue(item.MaterialId, out var price);
            return new PurchaseOrderDetail
            {
                PurchaseOrderDetailId = Guid.CreateVersion7(), PurchaseOrderId = purchaseOrderId,
                LineNo = lineNo++, MaterialId = item.MaterialId,
                MaterialExternalIDSnapshot = material.ExternalId, MaterialNameSnapshot = material.Name,
                Package = Normalize(item.Package) ?? material.Package, RequestQuantity = item.Quantity,
                BaseCostSnapshot = price?.CurrentPrice, BaseDateSnapshot = price?.UpdatedDate ?? price?.CreateDate,
                UnitPriceAgreed = item.UnitPriceAgreed, TotalPriceAgreed = item.Quantity * item.UnitPriceAgreed,
                DeliveryDate = item.DeliveryDate, Note = Normalize(item.Note), IsActive = true
            };
        }).ToList();
    }

    private async Task SyncSupplierPricesAsync(
        IEnumerable<PurchaseOrderLineRequest> items,
        IDictionary<Guid, MaterialsSupplier> existing,
        Guid supplierId,
        Guid employeeId,
        DateTime now,
        CancellationToken ct)
    {
        foreach (var item in items)
        {
            if (!existing.TryGetValue(item.MaterialId, out var link))
            {
                link = new MaterialsSupplier
                {
                    MaterialsSuppliersId = Guid.CreateVersion7(), SupplierId = supplierId,
                    MaterialId = item.MaterialId, CurrentPrice = item.UnitPriceAgreed, Currency = "VND",
                    CreateDate = now, CreatedBy = employeeId, IsActive = true, IsPreferred = false
                };
                await _dbContext.MaterialsSuppliers.AddAsync(link, ct);
                await _dbContext.PriceHistories.AddAsync(CreatePriceHistory(link.MaterialsSuppliersId, null, employeeId, now), ct);
                continue;
            }

            if (link.CurrentPrice == item.UnitPriceAgreed && string.Equals(link.Currency, "VND", StringComparison.OrdinalIgnoreCase))
                continue;

            await _dbContext.PriceHistories.AddAsync(CreatePriceHistory(link.MaterialsSuppliersId, link.CurrentPrice, employeeId, now), ct);
            link.CurrentPrice = item.UnitPriceAgreed; link.Currency = "VND";
            link.UpdatedDate = now; link.UpdatedBy = employeeId; link.IsActive = true;
        }
    }

    private static PriceHistory CreatePriceHistory(Guid linkId, decimal? oldPrice, Guid userId, DateTime now) => new()
    {
        PriceHistoryId = Guid.CreateVersion7(), MaterialsSuppliersId = linkId, OldPrice = oldPrice,
        Currency = "VND", CreateDate = now, CreatedBy = userId
    };

    private static PurchaseOrderSnapshot CreateSnapshot(CreatePurchaseOrderRequest request, SupplierSnapshot supplier, EmployeeSnapshot? employee, decimal totalPrice) => new()
    {
        PurchaseOrderSnapshotId = Guid.CreateVersion7(),
        EmployeeExternalIdSnapshot = employee?.ExternalId ?? string.Empty,
        EmployeeFullNameSnapshot = employee?.FullName ?? string.Empty,
        PhoneNumberSnapshot = employee?.PhoneNumber, EmailSnapshot = employee?.Email,
        SupplierExternalIdSnapshot = supplier.ExternalId, SupplierNameSnapshot = supplier.SupplierName,
        SupplierContactSnapshot = supplier.PrimaryContact, SupplierPhoneNumberSnapshot = supplier.Phone,
        SupplierAddressSnapshot = supplier.PrimaryAddress ?? supplier.RegistrationAddress,
        TotalPrice = totalPrice, DeliveryAddress = Normalize(request.DeliveryAddress),
        PaymentTypes = Normalize(request.PaymentTypes), Vat = request.Vat
    };

    private static string? Validate(CreatePurchaseOrderRequest request)
    {
        if (request.SupplierId == Guid.Empty) return "Nhà cung cấp là bắt buộc.";
        if (request.Items.Count == 0) return "PO phải có ít nhất một dòng vật tư.";
        if (request.Vat is < 0 or > 100) return "VAT phải nằm trong khoảng 0-100.";
        if (request.Items.Any(x => x.MaterialId == Guid.Empty || x.Quantity <= 0 || x.UnitPriceAgreed < 0)) return "Dòng vật tư có mã, số lượng hoặc đơn giá không hợp lệ.";
        return request.Items.Select(x => x.MaterialId).Distinct().Count() == request.Items.Count ? null : "Không được trùng vật tư trong một PO.";
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private sealed record MaterialSnapshot(Guid MaterialId, string? ExternalId, string? Name, string? Package);
    private sealed record MerchandiseOrderSnapshot(Guid MerchandiseOrderId, string ExternalId);
    private sealed record EmployeeSnapshot(string ExternalId, string FullName, string? PhoneNumber, string? Email);
    private sealed record SupplierSnapshot(Guid SupplierId, string? ExternalId, string? SupplierName, string? Phone, string? RegistrationAddress, string? PrimaryContact, string? PrimaryAddress);
}
