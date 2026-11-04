using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulaAdjustments;
using MediatR;

namespace HRM.Application.Features.PLM.Manufacturing.Queries.PreviewManufacturingAdjustmentPdf;

public sealed record PreviewManufacturingAdjustmentPdfQuery(Guid OrderId, ManufacturingAdjustmentInput Request, Guid? AdjustmentId = null) : IRequest<OperationResult<byte[]>>;
