using HRM.Application.Features.PLM.ManufacturingVUFormulas.Dtos;

namespace HRM.Application.Abstractions.Documents;

public interface IManufacturingVUFormulaPdfRenderer
{
    byte[] Render(ManufacturingVUFormulaPdfDocumentDto document);
}
