using HRM.Application.Features.PLM.Boms.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.Boms.Queries.GetBomVersion;

/// <summary>Lấy chi tiết một E-BOM version trong công ty hiện tại.</summary>
public sealed record GetBomVersionQuery(Guid BomVersionId)
    : IRequest<BomVersionDto?>;
