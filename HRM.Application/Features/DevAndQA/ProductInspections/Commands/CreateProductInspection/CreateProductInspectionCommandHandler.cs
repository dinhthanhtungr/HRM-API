using HRM.Application.Abstractions.Commons.ExternalIds;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.DevAndQA.ProductInspections.Dtos;
using HRM.Domain.Entities.DevandqaSchema;
using HRM.Domain.Enums.Category;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.DevAndQA.ProductInspections.Commands.CreateProductInspection;

internal sealed class CreateProductInspectionCommandHandler
    : IRequestHandler<CreateProductInspectionCommand, OperationResult<ProductInspectionWriteResultDto>>
{
    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IExternalIdService _externalIdService;

    public CreateProductInspectionCommandHandler(
        IPLMWriteDbContext dbContext,
        ICurrentUser currentUser,
        IExternalIdService externalIdService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _externalIdService = externalIdService;
    }

    public async Task<OperationResult<ProductInspectionWriteResultDto>> Handle(
        CreateProductInspectionCommand command,
        CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty)
            return OperationResult<ProductInspectionWriteResultDto>.Fail("Current company is invalid.");

        if (_currentUser.EmployeeId is not { } employeeId || employeeId == Guid.Empty)
            return OperationResult<ProductInspectionWriteResultDto>.Fail("Current user does not have an employee profile.");

        var validationError = ProductInspectionMutation.Validate(command.Request, creating: true);
        if (validationError is not null)
            return OperationResult<ProductInspectionWriteResultDto>.Fail(validationError);

        var order = command.Request.MfgProductionOrderId is { } orderId
            ? await _dbContext.MfgProductionOrders.AsNoTracking()
                .Where(x => x.MfgProductionOrderId == orderId && x.CompanyId == companyId &&
                    _dbContext.Products.Any(p => p.ProductId == x.ProductId && p.CompanyId == companyId))
                .Select(x => new { x.MfgProductionOrderId, x.ProductId, x.ProductExternalIdSnapshot,
                    ProductName = _dbContext.Products.Where(p => p.ProductId == x.ProductId).Select(p => p.Name).FirstOrDefault() })
                .FirstOrDefaultAsync(cancellationToken)
            : null;
        if (command.Request.MfgProductionOrderId.HasValue && order is null)
            return OperationResult<ProductInspectionWriteResultDto>.Fail("Production order was not found or is outside your company.");

        if (order is not null)
        {
            var batch = command.Request.BatchId!.Trim();
            var matchesBatch = await (from version in _dbContext.ProductionSelectVersions
                join formula in _dbContext.ManufacturingFormulas
                    on version.ManufacturingFormulaId equals formula.ManufacturingFormulaId
                where version.CompanyId == companyId && formula.CompanyId == companyId && formula.IsActive &&
                    version.MfgProductionOrderId == order.MfgProductionOrderId &&
                    (formula.ExternalId == batch || formula.ExternalId == "VA" + batch)
                select version.MfgProductionOrderId).AnyAsync(cancellationToken);
            if (!matchesBatch)
                return OperationResult<ProductInspectionWriteResultDto>.Fail("BatchId does not belong to the selected COA source.");
        }

        var standardId = command.Request.ProductStandardId;
        var standard = await _dbContext.ProductStandards
            .AsNoTracking()
            .Where(x => x.Id == standardId && x.CompanyId == companyId && x.IsActive)
            .Select(x => new
            {
                x.Id,
                x.ProductId,
                ProductCode = x.ProductExternalId ?? x.Product!.ColourCode,
                ProductName = x.Product!.Name
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (standardId.HasValue && standard is null)
            return OperationResult<ProductInspectionWriteResultDto>.Fail("Product standard was not found or is outside your company.");

        if (standard is not null && order is not null && standard.ProductId != order.ProductId)
            return OperationResult<ProductInspectionWriteResultDto>.Fail("Product standard does not belong to the selected production order's product.");

        var externalId = await _externalIdService.GenerateMonthlyCodeAsync(
            companyId,
            DocumentPrefix.KSP.ToString(),
            cancellationToken);

        var entity = new ProductInspection
        {
            Id = Guid.CreateVersion7(),
            CompanyId = companyId,
            ProductId = order?.ProductId ?? standard!.ProductId,
            MfgProductionOrderId = order?.MfgProductionOrderId,
            ProductStandardId = standard?.Id,
            ProductCode = standard?.ProductCode ?? order?.ProductExternalIdSnapshot,
            ProductName = standard?.ProductName ?? order?.ProductName,
            ExternalId = externalId,
            CreatedBy = _currentUser.UserName ?? employeeId.ToString(),
            CreateDate = DateTime.Now
        };

        ProductInspectionMutation.Apply(entity, command.Request);
        _dbContext.ProductInspections.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return OperationResult<ProductInspectionWriteResultDto>.Ok(
            new ProductInspectionWriteResultDto { Id = entity.Id, ExternalId = entity.ExternalId },
            "Created product inspection successfully.");
    }
}
