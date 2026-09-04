using HRM.Application.Features.PLM.Formulas.Dtos.Exports;

namespace HRM.Application.Abstractions.Documents;

public interface IFormulaMaterialsExcelRenderer
{
    byte[] Render(FormulaMaterialsExcelExportDocumentDto document);
}
