using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Application.Features.PLM.Boms.Mappers;
using HRM.Application.Features.PLM.Boms.Rules;
using HRM.Application.Features.PLM.Boms.Services;
using HRM.Domain.Entities.BomSchema;
using HRM.Domain.Enums.Boms;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Boms.Commands.CreateBom;

internal sealed class CreateBomCommandHandler
    : IRequestHandler<CreateBomCommand, OperationResult<BomVersionDto>>
{
    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly BomItemResolver _itemResolver;

    public CreateBomCommandHandler(
        IPLMWriteDbContext dbContext,
        ICurrentUser currentUser,
        BomItemResolver itemResolver)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _itemResolver = itemResolver;
    }

    public async Task<OperationResult<BomVersionDto>> Handle(
        CreateBomCommand command,
        CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty)
        {
            return OperationResult<BomVersionDto>.Fail("Current company is invalid.");
        }

        if (_currentUser.EmployeeId is not { } employeeId || employeeId == Guid.Empty)
        {
            return OperationResult<BomVersionDto>
                .Fail("Current user does not have an employee profile.");
        }

        var request = command.Request;
        var validationError = BomRules.ValidateCreateRequest(request);
        if (validationError is not null)
        {
            return OperationResult<BomVersionDto>.Fail(validationError);
        }

        var productExists = await _dbContext.Products
            .AsNoTracking()
            .AnyAsync(
                x => x.ProductId == request.ProductId &&
                     x.CompanyId == companyId &&
                     x.IsActive,
                cancellationToken);

        if (!productExists)
        {
            return OperationResult<BomVersionDto>
                .Fail("Product was not found or is outside your company.");
        }

        var normalizedCode = request.Code.Trim();
        var codeExists = await _dbContext.BomDefinitions
            .AsNoTracking()
            .AnyAsync(
                x => x.CompanyId == companyId && x.Code == normalizedCode,
                cancellationToken);

        if (codeExists)
        {
            return OperationResult<BomVersionDto>
                .Fail("BOM code already exists in your company.");
        }

        var resolution = await _itemResolver.ResolveAsync(
            request.ProductId,
            request.Items,
            companyId,
            cancellationToken);

        if (resolution.Error is not null)
        {
            return OperationResult<BomVersionDto>.Fail(resolution.Error);
        }

        var now = DateTime.UtcNow;
        var definition = CreateDefinition(request, companyId, employeeId, normalizedCode, now);
        var version = CreateInitialVersion(request, definition.BomDefinitionId, employeeId, now);
        var items = BomMapper.CreateVersionItems(version.BomVersionId, resolution.Items);

        await _dbContext.BomDefinitions.AddAsync(definition, cancellationToken);
        await _dbContext.BomVersions.AddAsync(version, cancellationToken);
        await _dbContext.BomVersionItems.AddRangeAsync(items, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return OperationResult<BomVersionDto>.Ok(
            BomMapper.ToWriteResponseDto(definition, version, items));
    }

    private static BomDefinition CreateDefinition(
        CreateBomRequest request,
        Guid companyId,
        Guid employeeId,
        string normalizedCode,
        DateTime now)
        => new()
        {
            BomDefinitionId = Guid.CreateVersion7(),
            CompanyId = companyId,
            ProductId = request.ProductId,
            Code = normalizedCode,
            Name = request.Name.Trim(),
            BomType = BomType.Engineering,
            Description = BomRules.NormalizeOptionalText(request.Description),
            IsActive = true,
            CreatedDate = now,
            CreatedBy = employeeId
        };

    private static BomVersion CreateInitialVersion(
        CreateBomRequest request,
        Guid definitionId,
        Guid employeeId,
        DateTime now)
        => new()
        {
            BomVersionId = Guid.CreateVersion7(),
            BomDefinitionId = definitionId,
            VersionNo = 1,
            Status = BomVersionStatus.Draft,
            BaseOutputQuantity = request.BaseOutputQuantity,
            OutputUnit = request.OutputUnit.Trim(),
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            Note = BomRules.NormalizeOptionalText(request.Note),
            CreatedDate = now,
            CreatedBy = employeeId
        };
}
