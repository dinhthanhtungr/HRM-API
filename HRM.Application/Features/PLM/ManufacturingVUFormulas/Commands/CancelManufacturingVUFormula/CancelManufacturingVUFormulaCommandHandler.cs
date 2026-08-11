using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.ManufacturingVUFormulas.Dtos;
using HRM.Domain.Enums.Manufacturings;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.ManufacturingVUFormulas.Commands.CancelManufacturingVUFormula;

/// <summary>
/// Hủy mềm lệnh sản xuất mẫu bằng trạng thái Canceled, không xóa snapshot vật tư.
/// </summary>
internal sealed class CancelManufacturingVUFormulaCommandHandler
    : IRequestHandler<
        CancelManufacturingVUFormulaCommand,
        OperationResult<ManufacturingVUFormulaWriteResultDto>>
{
    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public CancelManufacturingVUFormulaCommandHandler(
        IPLMWriteDbContext dbContext,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<OperationResult<ManufacturingVUFormulaWriteResultDto>> Handle(
        CancelManufacturingVUFormulaCommand request,
        CancellationToken cancellationToken)
    {
        if (request.ManufacturingVUFormulaId == Guid.Empty)
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

        var order = await _dbContext.ManufacturingVUFormulas
            .Where(x =>
                x.ManufacturingVUFormulaId == request.ManufacturingVUFormulaId &&
                x.Formula.Product.CompanyId == companyId)
            .FirstOrDefaultAsync(cancellationToken);

        if (order is null)
        {
            return OperationResult<ManufacturingVUFormulaWriteResultDto>
                .Fail("Sample production order was not found or is outside your company.");
        }

        if (order.status == ManufacturingProductOrder.Canceled)
        {
            return OperationResult<ManufacturingVUFormulaWriteResultDto>.Ok(
                ToResult(order),
                "Sample production order is already canceled.");
        }

        if (order.status is ManufacturingProductOrder.Finished
            or ManufacturingProductOrder.Done
            or ManufacturingProductOrder.Stocked)
        {
            return OperationResult<ManufacturingVUFormulaWriteResultDto>
                .Fail($"An order in status '{order.status}' cannot be canceled.");
        }

        order.status = ManufacturingProductOrder.Canceled;
        order.UpdatedBy = employeeId;
        order.UpdatedDate = DateTime.Now;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return OperationResult<ManufacturingVUFormulaWriteResultDto>.Ok(
            ToResult(order),
            "Canceled sample production order successfully.");
    }

    private static ManufacturingVUFormulaWriteResultDto ToResult(
        Domain.Entities.SampleRequestSchema.ManufacturingVUFormula order)
        => new()
        {
            ManufacturingVUFormulaId = order.ManufacturingVUFormulaId,
            Status = order.status,
            UpdatedDate = order.UpdatedDate
        };
}
