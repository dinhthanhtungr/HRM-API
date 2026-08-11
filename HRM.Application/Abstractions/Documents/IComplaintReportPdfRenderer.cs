using HRM.Application.Features.PLM.ComplaintReports.Dtos;

namespace HRM.Application.Abstractions.Documents;

public interface IComplaintReportPdfRenderer
{
    byte[] Render(ComplaintReportPdfDocumentDto document);
}
