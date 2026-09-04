using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.ColorChipRecords.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.ColorChipRecords.Queries.ExportColorChipRecordPdf;

public sealed record ExportColorChipRecordPdfQuery(Guid ProductId)
    : IRequest<OperationResult<ColorChipRecordPdfFileDto>>;
