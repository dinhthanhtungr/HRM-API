using HRM.Application.Abstractions.Commons.ExternalIds;
using HRM.Application.Abstractions.Persistence.PLM.SaleOrders;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.SaleOrders.Dtos;
using HRM.Application.Features.Timeline.Dtos;
using HRM.Application.Features.Timeline.Services;
using HRM.Domain.Entities.AttachmentSchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Enums.Category;
using HRM.Domain.Enums.Logs;
using HRM.Domain.Enums.Merchadises;
using HRM.Domain.Enums.Products;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SaleOrders.Commands.CreateSaleOrder.Services;

/// <summary>
/// Validate và dựng đầy đủ aggregate SaleOrder ở trạng thái New cùng các side effect trong DbContext.
/// Caller sở hữu transaction, SaveChanges và commit để có thể tái sử dụng trong create thường hoặc create kèm file.
/// </summary>
internal sealed class SaleOrderCreationService
{
    private readonly ISaleOrderDbContext _dbContext;
    private readonly IExternalIdService _externalIdService;
    private readonly IEventLogWriter _eventLogWriter;
    private readonly SaleOrderLeadConversionService _leadConversionService;

    public SaleOrderCreationService(
        ISaleOrderDbContext dbContext,
        IExternalIdService externalIdService,
        IEventLogWriter eventLogWriter,
        SaleOrderLeadConversionService leadConversionService)
    {
        _dbContext = dbContext;
        _externalIdService = externalIdService;
        _eventLogWriter = eventLogWriter;
        _leadConversionService = leadConversionService;
    }

    public async Task<OperationResult<MerchandiseOrder>> CreateAsync(
        CreateSaleOrderRequest request,
        Guid employeeId,
        Guid companyId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (request.CustomerId == Guid.Empty)
        {
            return OperationResult<MerchandiseOrder>.Fail("CustomerId không hợp lệ.");
        }

        if (!Enum.IsDefined(request.OrderType))
        {
            return OperationResult<MerchandiseOrder>.Fail("OrderType không hợp lệ.");
        }

        var details = request.SaleOrderDetails
            .Where(x => x.ProductId != Guid.Empty && x.FormulaId != Guid.Empty && x.ExpectedQuantity > 0)
            .ToList();
        if (details.Count == 0)
        {
            return OperationResult<MerchandiseOrder>.Fail(
                "Đơn hàng phải có ít nhất một dòng hợp lệ.");
        }

        if (details.Any(x => x.UnitPriceAgreed < 0))
        {
            return OperationResult<MerchandiseOrder>.Fail("UnitPriceAgreed không được âm.");
        }

        if (request.OrderType == OrderType.Merchandise &&
            details.Any(x => x.UnitPriceAgreed <= 0))
        {
            return OperationResult<MerchandiseOrder>.Fail(
                "Đơn Hàng hóa phải có UnitPriceAgreed lớn hơn 0.");
        }

        var managerResult = await ResolveOrderManagerAsync(
            request.CustomerId,
            companyId,
            employeeId,
            cancellationToken);
        if (!managerResult.Success || managerResult.Data is null)
        {
            return OperationResult<MerchandiseOrder>.Fail(
                managerResult.Message ?? "Không thể xác định sale phụ trách khách hàng.");
        }

        var detailFormulaIds = details.Select(x => x.FormulaId).Distinct().ToArray();
        var formulaRows = await _dbContext.Formulas
            .AsNoTracking()
            .Where(x =>
                detailFormulaIds.Contains(x.FormulaId) &&
                x.CompanyId == companyId &&
                x.IsActive)
            .Select(x => new
            {
                x.FormulaId,
                x.ProductId,
                x.ExternalId,
                x.Status
            })
            .ToListAsync(cancellationToken);
        var formulaById = formulaRows.ToDictionary(x => x.FormulaId);

        foreach (var detail in details)
        {
            if (!formulaById.TryGetValue(detail.FormulaId, out var formula))
            {
                return OperationResult<MerchandiseOrder>.Fail(
                    $"FormulaId không tồn tại hoặc không thuộc công ty hiện tại: {detail.FormulaId}.");
            }

            if (formula.ProductId != detail.ProductId)
            {
                return OperationResult<MerchandiseOrder>.Fail(
                    $"Công thức {formula.ExternalId} không thuộc sản phẩm của dòng đơn.");
            }

            if (formula.Status is not (nameof(FormulaStatus.SampleSent) or nameof(FormulaStatus.Completed)))
            {
                return OperationResult<MerchandiseOrder>.Fail(
                    $"Công thức {formula.ExternalId} phải ở trạng thái SampleSent hoặc Completed để lên đơn hàng.");
            }
        }

        var orderId = request.MerchandiseOrderId == Guid.Empty
            ? Guid.CreateVersion7()
            : request.MerchandiseOrderId;
        var attachmentCollectionId = request.AttachmentCollectionId;
        if (attachmentCollectionId == Guid.Empty)
        {
            attachmentCollectionId = Guid.CreateVersion7();
            await _dbContext.AttachmentCollections.AddAsync(new AttachmentCollection
            {
                AttachmentCollectionId = attachmentCollectionId
            }, cancellationToken);
        }
        else
        {
            var collectionExists = await _dbContext.AttachmentCollections
                .AsNoTracking()
                .AnyAsync(
                    x => x.AttachmentCollectionId == attachmentCollectionId,
                    cancellationToken);
            if (!collectionExists)
            {
                return OperationResult<MerchandiseOrder>.Fail(
                    "AttachmentCollectionId không tồn tại.");
            }
        }

        var order = new MerchandiseOrder
        {
            MerchandiseOrderId = orderId,
            ExternalId = await _externalIdService.GenerateMonthlyCodeAsync(
                companyId,
                DocumentPrefix.DHG.ToString(),
                cancellationToken),
            OrderType = request.OrderType,
            AttachmentCollectionId = attachmentCollectionId,
            CustomerId = request.CustomerId,
            CustomerNameSnapshot = TrimToEmpty(request.CustomerNameSnapshot),
            CustomerExternalIdSnapshot = TrimToEmpty(request.CustomerExternalIdSnapshot),
            PhoneSnapshot = TrimToEmpty(request.PhoneSnapshot),
            ManagerById = managerResult.Data.EmployeeId,
            ManagerByNameSnapshot = managerResult.Data.FullName,
            ManagerExternalIdSnapshot = managerResult.Data.ExternalId,
            Receiver = TrimToEmpty(request.Receiver),
            DeliveryAddress = TrimToEmpty(request.DeliveryAddress),
            PaymentType = TrimToNull(request.PaymentType),
            Vat = request.Vat,
            Status = MerchadiseStatus.New.ToString(),
            Currency = TrimToNull(request.Currency),
            ExchangeRate = request.ExchangeRate,
            CompanyId = companyId,
            IsPaid = request.IsPaid,
            IsActive = true,
            PaymentDate = request.PaymentDate,
            Note = TrimToNull(request.Note),
            ShippingMethod = TrimToNull(request.ShippingMethod),
            PONo = TrimToEmpty(request.PONo),
            CreateDate = now,
            CreatedBy = employeeId,
            UpdatedDate = now,
            UpdatedBy = employeeId
        };

        foreach (var detailRequest in details)
        {
            var totalPrice = Math.Round(
                detailRequest.UnitPriceAgreed * detailRequest.ExpectedQuantity,
                2,
                MidpointRounding.AwayFromZero);
            order.MerchandiseOrderDetails.Add(new MerchandiseOrderDetail
            {
                MerchandiseOrderDetailId = detailRequest.MerchandiseOrderDetailId == Guid.Empty
                    ? Guid.CreateVersion7()
                    : detailRequest.MerchandiseOrderDetailId,
                MerchandiseOrderId = orderId,
                ProductId = detailRequest.ProductId,
                ProductExternalIdSnapshot = TrimToEmpty(detailRequest.ProductExternalIdSnapshot),
                ProductNameSnapshot = TrimToEmpty(detailRequest.ProductNameSnapshot),
                FormulaId = detailRequest.FormulaId,
                FormulaExternalIdSnapshot = TrimToEmpty(detailRequest.FormulaExternalIdSnapshot),
                ExpectedQuantity = detailRequest.ExpectedQuantity,
                RealQuantity = detailRequest.RealQuantity,
                BagType = TrimToEmpty(detailRequest.BagType),
                PackageWeight = TrimToEmpty(detailRequest.PackageWeight),
                Status = order.Status,
                Comment = TrimToNull(detailRequest.Comment),
                DeliveryRequestDate = detailRequest.DeliveryRequestDate,
                DeliveryActualDate = detailRequest.DeliveryActualDate,
                ExpectedDeliveryDate = detailRequest.ExpectedDeliveryDate,
                BaseCostSnapshot = detailRequest.BaseCostSnapshot,
                RecommendedUnitPrice = detailRequest.RecommendedUnitPrice,
                UnitPriceAgreed = detailRequest.UnitPriceAgreed,
                TotalPriceAgreed = totalPrice,
                IsActive = true
            });
        }

        order.TotalPrice = Math.Round(
            order.MerchandiseOrderDetails.Sum(x => x.TotalPriceAgreed),
            2,
            MidpointRounding.AwayFromZero);

        await _leadConversionService.ApplyAsync(order, employeeId, now, cancellationToken);
        await _dbContext.MerchandiseOrders.AddAsync(order, cancellationToken);
        await _eventLogWriter.AddAsync(new EventLogCreateRequest
        {
            EmployeeId = employeeId,
            CompanyId = order.CompanyId,
            SourceType = "MerchandiseOrder",
            SourceId = order.MerchandiseOrderId,
            SourceCode = order.ExternalId,
            EventType = EventType.MerchadiseStatus,
            Status = order.Status,
            Note = $"Created Merchandise Order {order.ExternalId}",
            CreatedDate = now
        }, cancellationToken);

        return OperationResult<MerchandiseOrder>.Ok(order);
    }

    public static CreateSaleOrderResultDto ToResultDto(MerchandiseOrder order)
    {
        return new CreateSaleOrderResultDto
        {
            MerchandiseOrderId = order.MerchandiseOrderId,
            ExternalId = order.ExternalId,
            AttachmentCollectionId = order.AttachmentCollectionId,
            Status = order.Status
        };
    }

    private static string TrimToEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string? TrimToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Resolve the sale responsible for the customer from CRM data, never from client fields.
    /// </summary>
    private async Task<OperationResult<OrderManagerSnapshot>> ResolveOrderManagerAsync(
        Guid customerId,
        Guid companyId,
        Guid creatingEmployeeId,
        CancellationToken cancellationToken)
    {
        var customer = await _dbContext.Customers
            .AsNoTracking()
            .Where(x =>
                x.CustomerId == customerId &&
                x.CompanyId == companyId &&
                x.IsActive != false)
            .Select(x => new
            {
                x.IsLead,
                x.CurrentSaleId,
                LatestAssignmentEmployeeId = x.CustomerAssignments
                    .Where(assignment => assignment.IsActive && assignment.CompanyId == companyId)
                    .OrderByDescending(assignment => assignment.CreatedDate)
                    .Select(assignment => (Guid?)assignment.EmployeeId)
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (customer is null)
        {
            return OperationResult<OrderManagerSnapshot>.Fail(
                "Không tìm thấy khách hàng trong công ty hiện tại.");
        }

        var resolvedManagerId = customer.IsLead
            ? creatingEmployeeId
            : customer.LatestAssignmentEmployeeId ?? customer.CurrentSaleId ?? creatingEmployeeId;

        var manager = await FindActiveEmployeeAsync(
            resolvedManagerId,
            companyId,
            cancellationToken);
        if (manager is not null)
        {
            return OperationResult<OrderManagerSnapshot>.Ok(manager);
        }

        if (resolvedManagerId != creatingEmployeeId)
        {
            manager = await FindActiveEmployeeAsync(
                creatingEmployeeId,
                companyId,
                cancellationToken);
            if (manager is not null)
            {
                return OperationResult<OrderManagerSnapshot>.Ok(manager);
            }
        }

        return OperationResult<OrderManagerSnapshot>.Fail(
            "Không tìm thấy sale phụ trách hoạt động trong công ty hiện tại.");
    }

    private Task<OrderManagerSnapshot?> FindActiveEmployeeAsync(
        Guid employeeId,
        Guid companyId,
        CancellationToken cancellationToken)
        => _dbContext.Employees
            .AsNoTracking()
            .Where(x =>
                x.EmployeeId == employeeId &&
                x.CompanyId == companyId &&
                x.IsActive)
            .Select(x => new OrderManagerSnapshot(x.EmployeeId, x.FullName, x.ExternalId))
            .FirstOrDefaultAsync(cancellationToken);

    private sealed record OrderManagerSnapshot(Guid EmployeeId, string FullName, string ExternalId);
}
