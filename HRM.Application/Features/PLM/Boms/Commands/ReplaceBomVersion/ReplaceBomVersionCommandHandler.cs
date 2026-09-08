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

namespace HRM.Application.Features.PLM.Boms.Commands.ReplaceBomVersion;

internal sealed class ReplaceBomVersionCommandHandler
    : IRequestHandler<ReplaceBomVersionCommand, OperationResult<BomVersionDto>>
{
    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly BomItemResolver _itemResolver;

    public ReplaceBomVersionCommandHandler(
        IPLMWriteDbContext dbContext,
        ICurrentUser currentUser,
        BomItemResolver itemResolver)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _itemResolver = itemResolver;
    }

    public async Task<OperationResult<BomVersionDto>> Handle(
        ReplaceBomVersionCommand command,
        CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty ||
            _currentUser.EmployeeId is not { } employeeId || employeeId == Guid.Empty)
        {
            return OperationResult<BomVersionDto>
                .Fail("Current company or employee is invalid.");
        }

        var request = command.Request;
        var validationError = BomRules.ValidateReplaceRequest(request);
        if (validationError is not null)
        {
            return OperationResult<BomVersionDto>.Fail(validationError);
        }

        var version = await _dbContext.BomVersions
            .Include(x => x.BomDefinition)
            .Include(x => x.Items)
            .FirstOrDefaultAsync(
                x => x.BomVersionId == command.BomVersionId &&
                     x.BomDefinition.CompanyId == companyId &&
                     x.BomDefinition.BomType == BomType.Engineering,
                cancellationToken);

        if (version is null)
        {
            return OperationResult<BomVersionDto>.Fail("BOM version was not found.");
        }

        if (version.Status != BomVersionStatus.Draft)
        {
            return OperationResult<BomVersionDto>
                .Fail("Only Draft BOM versions can be changed.");
        }

        var resolution = await _itemResolver.ResolveAsync(
            version.BomDefinition.ProductId,
            request.Items,
            companyId,
            cancellationToken);

        if (resolution.Error is not null)
        {
            return OperationResult<BomVersionDto>.Fail(resolution.Error);
        }

        _dbContext.BomVersionItems.RemoveRange(version.Items);
        ApplyReplacement(version, request);

        var replacementItems = BomMapper.CreateVersionItems(
            version.BomVersionId,
            resolution.Items);

        await _dbContext.BomVersionItems.AddRangeAsync(replacementItems, cancellationToken);

        version.BomDefinition.UpdatedBy = employeeId;
        version.BomDefinition.UpdatedDate = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return OperationResult<BomVersionDto>.Ok(
            BomMapper.ToWriteResponseDto(
                version.BomDefinition,
                version,
                replacementItems));
    }

    private static void ApplyReplacement(
        BomVersion version,
        ReplaceBomVersionRequest request)
    {
        version.BaseOutputQuantity = request.BaseOutputQuantity;
        version.OutputUnit = request.OutputUnit.Trim();
        version.EffectiveFrom = request.EffectiveFrom;
        version.EffectiveTo = request.EffectiveTo;
        version.ChangeReason = BomRules.NormalizeOptionalText(request.ChangeReason);
        version.Note = BomRules.NormalizeOptionalText(request.Note);
    }
}
