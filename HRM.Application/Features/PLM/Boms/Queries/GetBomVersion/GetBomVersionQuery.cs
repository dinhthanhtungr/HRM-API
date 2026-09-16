using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Domain.Enums.Boms;
using MediatR;

namespace HRM.Application.Features.PLM.Boms.Queries.GetBomVersion;

/// <summary>Lấy chi tiết một BOM version theo loại mong đợi trong công ty hiện tại.</summary>
public sealed record GetBomVersionQuery(Guid BomVersionId, BomType? ExpectedBomType = null)
    : IRequest<BomVersionDto?>;
