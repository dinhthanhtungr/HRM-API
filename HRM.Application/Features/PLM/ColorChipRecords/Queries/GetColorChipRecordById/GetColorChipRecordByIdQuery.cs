using HRM.Application.Features.PLM.ColorChipRecords.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.ColorChipRecords.Queries.GetColorChipRecordById;

/// <summary>Lấy hồ sơ Color Chip active theo id trong phạm vi công ty hiện tại.</summary>
public sealed record GetColorChipRecordByIdQuery(Guid ColorChipRecordId) : IRequest<ColorChipRecordDto?>;
