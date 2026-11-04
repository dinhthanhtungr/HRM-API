using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulaAdjustments;
using HRM.Application.Features.PLM.Manufacturing.Services.Adjustments;
using MediatR;

namespace HRM.Application.Features.PLM.Manufacturing.Queries.ExportManufacturingAdjustmentPdf;

internal sealed class ExportManufacturingAdjustmentPdfQueryHandler(ManufacturingAdjustmentDocumentService documents) : IRequestHandler<ExportManufacturingAdjustmentPdfQuery, OperationResult<byte[]>>
{
    public Task<OperationResult<byte[]>> Handle(ExportManufacturingAdjustmentPdfQuery request, CancellationToken ct) => documents.PrintAsync(request.Id, request.BatchNo, ct);
}
