using HRM.Application.Features.DevAndQA.ProductInspections.Dtos;

namespace HRM.Application.Abstractions.Documents;

public interface IProductInspectionPdfRenderer
{
    byte[] Render(ProductInspectionPdfDocumentDto document, bool templateOnly);
}
