using HRM.Application.Features.PLM.Manufacturing.Dtos;

namespace HRM.Application.Abstractions.Documents;

public interface IManufacturingOrderPdfRenderer
{
    byte[] Render(ManufacturingOrderPdfDocument document, bool templateOnly = false);
}
