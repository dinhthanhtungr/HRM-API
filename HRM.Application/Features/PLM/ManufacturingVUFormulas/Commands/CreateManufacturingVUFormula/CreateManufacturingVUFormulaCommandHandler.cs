using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.ManufacturingVUFormulas.Dtos;
using HRM.Application.Features.PLM.ManufacturingVUFormulas.Rules;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.Manufacturings;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.ManufacturingVUFormulas.Commands.CreateManufacturingVUFormula;

/// <summary>
/// Tạo lệnh sản xuất mẫu VU và chụp lại định mức vật tư tại thời điểm tạo.
/// </summary>
internal sealed class CreateManufacturingVUFormulaCommandHandler
    : IRequestHandler<
        CreateManufacturingVUFormulaCommand,
        OperationResult<ManufacturingVUFormulaWriteResultDto>>
{
    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public CreateManufacturingVUFormulaCommandHandler(
        IPLMWriteDbContext dbContext,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<OperationResult<ManufacturingVUFormulaWriteResultDto>> Handle(
        CreateManufacturingVUFormulaCommand command,
        CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty)
        {
            return OperationResult<ManufacturingVUFormulaWriteResultDto>
                .Fail("Current company is invalid.");
        }

        if (_currentUser.EmployeeId is not { } employeeId || employeeId == Guid.Empty)
        {
            return OperationResult<ManufacturingVUFormulaWriteResultDto>
                .Fail("Current user does not have an employee profile.");
        }

        var request = command.Request;
        var validationError = Validate(request);
        if (validationError is not null)
        {
            return OperationResult<ManufacturingVUFormulaWriteResultDto>.Fail(validationError);
        }

        var formula = await _dbContext.Formulas
            .AsNoTracking()
            .Where(x =>
                x.FormulaId == request.FormulaId &&
                x.IsActive &&
                x.Product.IsActive &&
                x.Product.CompanyId == companyId)
            .Select(x => new
            {
                x.FormulaId
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (formula is null)
        {
            return OperationResult<ManufacturingVUFormulaWriteResultDto>
                .Fail("Formula was not found or is outside your company.");
        }

        var materialRows = await _dbContext.FormulaMaterials
            .AsNoTracking()
            .Where(x => x.FormulaId == request.FormulaId && x.IsActive)
            .OrderBy(x => x.LineNo)
            .ThenBy(x => x.FormulaMaterialId)
            .Select(x => new
            {
                x.CategoryId,
                x.Quantity,
                x.UnitPrice,
                x.TotalPrice,
                x.itemType,
                x.LineNo,
                x.MaterialNameSnapshot,
                x.MaterialExternalIdSnapshot,
                x.Unit
            })
            .ToListAsync(cancellationToken);

        if (materialRows.Count == 0)
        {
            return OperationResult<ManufacturingVUFormulaWriteResultDto>
                .Fail("Formula does not have any active materials.");
        }

        var now = DateTime.Now;
        var order = new ManufacturingVUFormula
        {
            ManufacturingVUFormulaId = Guid.CreateVersion7(),
            FormulaId = formula.FormulaId,
            status = ManufacturingProductOrder.New,
            TotalProductionQuantity = request.TotalProductionQuantity,
            NumOfBatches = request.NumOfBatches,
            LabNote = ManufacturingVUFormulaRules.NormalizeOptionalText(request.LabNote),
            Requirement = ManufacturingVUFormulaRules.NormalizeOptionalText(request.Requirement),
            QcCheck = ManufacturingVUFormulaRules.NormalizeOptionalText(request.QcCheck),
            CreatedDate = now,
            CreatedBy = employeeId,
            UpdatedDate = now,
            UpdatedBy = employeeId
        };

        await _dbContext.ManufacturingVUFormulas.AddAsync(order, cancellationToken);
        await _dbContext.FormulaMaterialSnapshots.AddRangeAsync(
            materialRows.Select(x => new FormulaMaterialSnapshot
            {
                FormulaMaterialSnapshotId = Guid.CreateVersion7(),
                ManufacturingVUFormulaId = order.ManufacturingVUFormulaId,
                CategoryId = x.CategoryId,
                Quantity = x.Quantity,
                UnitPrice = x.UnitPrice,
                TotalPrice = x.TotalPrice,
                itemType = x.itemType,
                LineNo = x.LineNo,
                MaterialNameSnapshot = x.MaterialNameSnapshot,
                MaterialExternalIdSnapshot = x.MaterialExternalIdSnapshot,
                Unit = x.Unit,
                IsActive = true
            }),
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return OperationResult<ManufacturingVUFormulaWriteResultDto>.Ok(
            ToResult(order),
            "Created sample production order successfully.");
    }

    private static string? Validate(CreateManufacturingVUFormulaRequest request)
    {
        if (request.FormulaId == Guid.Empty)
        {
            return "FormulaId is required.";
        }

        if (request.TotalProductionQuantity <= 0)
        {
            return "TotalProductionQuantity must be greater than zero.";
        }

        if (request.NumOfBatches <= 0)
        {
            return "NumOfBatches must be greater than zero.";
        }

        return ManufacturingVUFormulaRules.ValidateText(request.LabNote, nameof(request.LabNote))
            ?? ManufacturingVUFormulaRules.ValidateText(request.Requirement, nameof(request.Requirement))
            ?? ManufacturingVUFormulaRules.ValidateText(request.QcCheck, nameof(request.QcCheck));
    }

    private static ManufacturingVUFormulaWriteResultDto ToResult(ManufacturingVUFormula order)
        => new()
        {
            ManufacturingVUFormulaId = order.ManufacturingVUFormulaId,
            Status = order.status,
            UpdatedDate = order.UpdatedDate
        };
}
