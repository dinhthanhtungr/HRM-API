using HRM.Application.Features.CRM.Quotations.Dtos;

namespace HRM.Application.Abstractions.Documents;

public interface IQuotationPdfRenderer
{
    byte[] Render(QuotationPdfDocumentDto quotation);
}
