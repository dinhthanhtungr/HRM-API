using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Patching;
using HRM.Application.Features.PLM.ManufacturingVUFormulas.Dtos;
using HRM.Application.Features.PLM.ManufacturingVUFormulas.Rules;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.ManufacturingVUFormulas.Commands.PatchManufacturingVUFormula;

internal sealed class PatchManufacturingVUFormulaCommandHandler
    : IRequestHandler<
        PatchManufacturingVUFormulaCommand,
        OperationResult<ManufacturingVUFormulaWriteResultDto>>
{
    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public PatchManufacturingVUFormulaCommandHandler(
        IPLMWriteDbContext dbContext,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<OperationResult<ManufacturingVUFormulaWriteResultDto>> Handle(
        PatchManufacturingVUFormulaCommand command,
        CancellationToken cancellationToken)
    {
        if (command.ManufacturingVUFormulaId == Guid.Empty)
        {
            return OperationResult<ManufacturingVUFormulaWriteResultDto>
                .Fail("ManufacturingVUFormulaId is invalid.");
        }

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

        var order = await _dbContext.ManufacturingVUFormulas
            .Where(x =>
                x.ManufacturingVUFormulaId == command.ManufacturingVUFormulaId &&
                x.Formula.Product.CompanyId == companyId)
            .FirstOrDefaultAsync(cancellationToken);

        if (order is null)
        {
            return OperationResult<ManufacturingVUFormulaWriteResultDto>
                .Fail("Sample production order was not found or is outside your company.");
        }

        if (ManufacturingVUFormulaRules.IsTerminal(order.status))
        {
            return OperationResult<ManufacturingVUFormulaWriteResultDto>
                .Fail($"An order in status '{order.status}' can no longer be updated.");
        }

        var changed = false;
        changed |= PatchHelper.SetIfHasValue(
            request.TotalProductionQuantity,
            () => order.TotalProductionQuantity ?? 0m,
            value => order.TotalProductionQuantity = value);
        changed |= PatchHelper.SetIfHasValue(
            request.NumOfBatches,
            () => order.NumOfBatches ?? 0,
            value => order.NumOfBatches = value);
        changed |= PatchHelper.SetTrimmed(
            request.LabNote,
            () => order.LabNote,
            value => order.LabNote = value);
        changed |= PatchHelper.SetTrimmed(
            request.Requirement,
            () => order.Requirement,
            value => order.Requirement = value);
        changed |= PatchHelper.SetTrimmed(
            request.QcCheck,
            () => order.QcCheck,
            value => order.QcCheck = value);

        if (request.Status is { } status && status != order.status)
        {
            order.status = status;
            changed = true;
        }

        if (changed)
        {
            order.UpdatedBy = employeeId;
            order.UpdatedDate = DateTime.Now;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return OperationResult<ManufacturingVUFormulaWriteResultDto>.Ok(
            new ManufacturingVUFormulaWriteResultDto
            {
                ManufacturingVUFormulaId = order.ManufacturingVUFormulaId,
                Status = order.status,
                UpdatedDate = order.UpdatedDate
            },
            changed
                ? "Updated sample production order successfully."
                : "No changes detected.");
    }

    private static string? Validate(PatchManufacturingVUFormulaRequest request)
    {
        if (request.TotalProductionQuantity is <= 0)
        {
            return "TotalProductionQuantity must be greater than zero.";
        }

        if (request.NumOfBatches is <= 0)
        {
            return "NumOfBatches must be greater than zero.";
        }

        if (request.Status is { } status &&
            !ManufacturingVUFormulaRules.IsUpdatableStatus(status))
        {
            return "Status is invalid. Use the cancel endpoint to cancel an order.";
        }

        return ManufacturingVUFormulaRules.ValidateText(request.LabNote, nameof(request.LabNote))
            ?? ManufacturingVUFormulaRules.ValidateText(request.Requirement, nameof(request.Requirement))
            ?? ManufacturingVUFormulaRules.ValidateText(request.QcCheck, nameof(request.QcCheck));
    }
}
