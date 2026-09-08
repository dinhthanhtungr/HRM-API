using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Domain.Enums.Boms;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Boms.Queries.GetBomVersion;

internal sealed class GetBomVersionQueryHandler
    : IRequestHandler<GetBomVersionQuery, BomVersionDto?>
{
    private readonly IPLMReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public GetBomVersionQueryHandler(
        IPLMReadDbContext dbContext,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<BomVersionDto?> Handle(
        GetBomVersionQuery request,
        CancellationToken cancellationToken)
    {
        if (request.BomVersionId == Guid.Empty ||
            _currentUser.CompanyId is not { } companyId ||
            companyId == Guid.Empty)
        {
            return null;
        }

        return await _dbContext.BomVersions
            .AsNoTracking()
            .Where(x =>
                x.BomVersionId == request.BomVersionId &&
                x.BomDefinition.CompanyId == companyId &&
                x.BomDefinition.BomType == BomType.Engineering &&
                x.BomDefinition.IsActive)
            .Select(x => new BomVersionDto
            {
                BomDefinitionId = x.BomDefinitionId,
                BomVersionId = x.BomVersionId,
                ProductId = x.BomDefinition.ProductId,
                Code = x.BomDefinition.Code,
                Name = x.BomDefinition.Name,
                BomType = x.BomDefinition.BomType,
                VersionNo = x.VersionNo,
                Status = x.Status,
                BaseOutputQuantity = x.BaseOutputQuantity,
                OutputUnit = x.OutputUnit,
                EffectiveFrom = x.EffectiveFrom,
                EffectiveTo = x.EffectiveTo,
                ChangeReason = x.ChangeReason,
                Note = x.Note,
                Items = x.Items
                    .OrderBy(item => item.LineNo)
                    .Select(item => new BomItemDto
                    {
                        BomVersionItemId = item.BomVersionItemId,
                        LineNo = item.LineNo,
                        ItemType = item.ItemType,
                        ItemId = item.MaterialId ?? item.ComponentProductId ?? Guid.Empty,
                        CategoryId = item.CategoryId,
                        Quantity = item.Quantity,
                        Unit = item.Unit,
                        ItemCode = item.MaterialExternalIdSnapshot,
                        ItemName = item.MaterialNameSnapshot,
                        Note = item.Note
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);
    }
}
