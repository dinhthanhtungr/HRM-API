using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulaAdjustments;

namespace HRM.Application.Abstractions.Documents;

public interface IManufacturingAdjustmentPdfRenderer
{
    byte[] Render(PdfMfgFormulaAdjustment document);
}
