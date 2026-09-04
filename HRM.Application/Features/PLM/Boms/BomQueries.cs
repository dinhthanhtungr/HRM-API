using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Domain.Enums.Boms;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Boms;

public sealed class GetBomsQuery : IRequest<IReadOnlyList<BomListItemDto>>
{
    public Guid? ProductId { get; init; }
    public string? Keyword { get; init; }
}
public sealed record GetBomVersionQuery(Guid BomVersionId) : IRequest<BomVersionDto?>;

internal sealed class GetBomsQueryHandler : IRequestHandler<GetBomsQuery, IReadOnlyList<BomListItemDto>>
{
    private readonly IPLMReadDbContext _db; private readonly ICurrentUser _user;
    public GetBomsQueryHandler(IPLMReadDbContext db, ICurrentUser user) => (_db, _user) = (db, user);
    public async Task<IReadOnlyList<BomListItemDto>> Handle(GetBomsQuery request, CancellationToken ct)
    {
        if (_user.CompanyId is not { } companyId || companyId == Guid.Empty) return [];
        var keyword = request.Keyword?.Trim();
        return await _db.BomDefinitions.AsNoTracking().Where(x => x.CompanyId == companyId && x.BomType == BomType.Engineering && x.IsActive && (!request.ProductId.HasValue || x.ProductId == request.ProductId) && (string.IsNullOrEmpty(keyword) || x.Code.Contains(keyword) || x.Name.Contains(keyword))).OrderBy(x => x.Code).Select(x => new BomListItemDto { BomDefinitionId = x.BomDefinitionId, ProductId = x.ProductId, Code = x.Code, Name = x.Name, BomType = x.BomType, IsActive = x.IsActive, VersionCount = x.Versions.Count }).ToListAsync(ct);
    }
}

internal sealed class GetBomVersionQueryHandler : IRequestHandler<GetBomVersionQuery, BomVersionDto?>
{
    private readonly IPLMReadDbContext _db; private readonly ICurrentUser _user;
    public GetBomVersionQueryHandler(IPLMReadDbContext db, ICurrentUser user) => (_db, _user) = (db, user);
    public async Task<BomVersionDto?> Handle(GetBomVersionQuery request, CancellationToken ct)
    {
        if (request.BomVersionId == Guid.Empty || _user.CompanyId is not { } companyId || companyId == Guid.Empty) return null;
        return await _db.BomVersions.AsNoTracking().Where(x => x.BomVersionId == request.BomVersionId && x.BomDefinition.CompanyId == companyId && x.BomDefinition.BomType == BomType.Engineering && x.BomDefinition.IsActive).Select(x => new BomVersionDto { BomDefinitionId = x.BomDefinitionId, BomVersionId = x.BomVersionId, ProductId = x.BomDefinition.ProductId, Code = x.BomDefinition.Code, Name = x.BomDefinition.Name, BomType = x.BomDefinition.BomType, VersionNo = x.VersionNo, Status = x.Status, BaseOutputQuantity = x.BaseOutputQuantity, OutputUnit = x.OutputUnit, EffectiveFrom = x.EffectiveFrom, EffectiveTo = x.EffectiveTo, ChangeReason = x.ChangeReason, Note = x.Note, Items = x.Items.OrderBy(i => i.LineNo).Select(i => new BomItemDto { BomVersionItemId = i.BomVersionItemId, LineNo = i.LineNo, ItemType = i.ItemType, ItemId = i.MaterialId ?? i.ComponentProductId ?? Guid.Empty, CategoryId = i.CategoryId, Quantity = i.Quantity, Unit = i.Unit, ItemCode = i.MaterialExternalIdSnapshot, ItemName = i.MaterialNameSnapshot, Note = i.Note }).ToList() }).FirstOrDefaultAsync(ct);
    }
}
