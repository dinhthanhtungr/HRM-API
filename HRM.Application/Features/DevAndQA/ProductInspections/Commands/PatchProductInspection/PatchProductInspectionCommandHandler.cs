using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Patching;
using HRM.Application.Features.DevAndQA.ProductInspections.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.DevAndQA.ProductInspections.Commands.PatchProductInspection;

internal sealed class PatchProductInspectionCommandHandler
    : IRequestHandler<PatchProductInspectionCommand, OperationResult<ProductInspectionWriteResultDto>>
{
    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public PatchProductInspectionCommandHandler(IPLMWriteDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<OperationResult<ProductInspectionWriteResultDto>> Handle(
        PatchProductInspectionCommand command,
        CancellationToken cancellationToken)
    {
        if (command.Id == Guid.Empty)
            return OperationResult<ProductInspectionWriteResultDto>.Fail("Id is invalid.");

        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty)
            return OperationResult<ProductInspectionWriteResultDto>.Fail("Current company is invalid.");

        if (_currentUser.EmployeeId is not { } employeeId || employeeId == Guid.Empty)
            return OperationResult<ProductInspectionWriteResultDto>.Fail("Current user does not have an employee profile.");

        var validationError = ProductInspectionMutation.Validate(command.Request, creating: false);
        if (validationError is not null)
            return OperationResult<ProductInspectionWriteResultDto>.Fail(validationError);

        var entity = await _dbContext.ProductInspections
            .Where(x => x.Id == command.Id &&
                x.ProductStandardId.HasValue &&
                _dbContext.ProductStandards.Any(s =>
                    s.Id == x.ProductStandardId.Value && s.CompanyId == companyId))
            .FirstOrDefaultAsync(cancellationToken);

        if (entity is null)
            return OperationResult<ProductInspectionWriteResultDto>.Fail("Product inspection was not found or is outside your company.");

        var changed = false;
        if (command.Request.ProductStandardId is { } newStandardId && newStandardId != entity.ProductStandardId)
        {
            var standard = await _dbContext.ProductStandards
                .AsNoTracking()
                .Where(x => x.Id == newStandardId && x.CompanyId == companyId && x.IsActive)
                .Select(x => new
                {
                    x.Id,
                    ProductCode = x.ProductExternalId ?? x.Product!.ColourCode,
                    ProductName = x.Product!.Name
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (standard is null)
                return OperationResult<ProductInspectionWriteResultDto>.Fail("Product standard was not found or is outside your company.");

            changed |= PatchHelper.SetNullable<Guid>(standard.Id, () => entity.ProductStandardId, value => entity.ProductStandardId = value);
            changed |= PatchHelper.SetNullableRef(standard.ProductCode, () => entity.ProductCode, value => entity.ProductCode = value);
            changed |= PatchHelper.SetNullableRef(standard.ProductName, () => entity.ProductName, value => entity.ProductName = value);
        }

        changed |= ProductInspectionMutation.Apply(entity, command.Request);
        if (changed)
            await _dbContext.SaveChangesAsync(cancellationToken);

        return OperationResult<ProductInspectionWriteResultDto>.Ok(
            new ProductInspectionWriteResultDto { Id = entity.Id, ExternalId = entity.ExternalId },
            changed ? "Updated product inspection successfully." : "No changes detected.");
    }
}
