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

        var standardId = command.Request.ProductStandardId!.Value;
        var standard = await _dbContext.ProductStandards
            .AsNoTracking()
            .Where(x => x.Id == standardId && x.CompanyId == companyId && x.IsActive)
            .Select(x => new
            {
                x.Id,
                ProductCode = x.ProductExternalId ?? x.Product!.ColourCode,
                ProductName = x.Product!.Name
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (standard is null)
            return OperationResult<ProductInspectionWriteResultDto>.Fail("Product standard was not found or is outside your company.");

        var externalId = await _externalIdService.GenerateMonthlyCodeAsync(
            companyId,
            DocumentPrefix.KSP.ToString(),
            cancellationToken);

        var entity = new ProductInspection
        {
            Id = Guid.CreateVersion7(),
            ProductStandardId = standard.Id,
            ProductCode = standard.ProductCode,
            ProductName = standard.ProductName,
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
