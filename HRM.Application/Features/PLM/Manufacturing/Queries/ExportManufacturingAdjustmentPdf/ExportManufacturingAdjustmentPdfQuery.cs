using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulaAdjustments;
using MediatR;

namespace HRM.Application.Features.PLM.Manufacturing.Queries.ExportManufacturingAdjustmentPdf;

public sealed record ExportManufacturingAdjustmentPdfQuery(Guid Id, int? BatchNo = null) : IRequest<OperationResult<byte[]>>;
