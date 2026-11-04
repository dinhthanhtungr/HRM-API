using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulaAdjustments;
using HRM.Application.Features.PLM.Manufacturing.Services.Adjustments;
using MediatR;

namespace HRM.Application.Features.PLM.Manufacturing.Queries.PreviewManufacturingAdjustmentPdf;

internal sealed class PreviewManufacturingAdjustmentPdfQueryHandler(ManufacturingAdjustmentDocumentService documents) : IRequestHandler<PreviewManufacturingAdjustmentPdfQuery, OperationResult<byte[]>>
{
    public Task<OperationResult<byte[]>> Handle(PreviewManufacturingAdjustmentPdfQuery request, CancellationToken ct) => documents.PreviewAsync(request.OrderId, request.Request, ct, request.AdjustmentId);
}
