using HRM.Application.Features.PLM.ColorChipRecords.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.ColorChipRecords.Queries.GetColorChipRecordByProductId;

/// <summary>Lấy hồ sơ Color Chip active mới nhất của một Product trong phạm vi công ty hiện tại.</summary>
public sealed record GetColorChipRecordByProductIdQuery(Guid ProductId) : IRequest<ColorChipRecordDto?>;
