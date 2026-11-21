using HRM.Application.Features.PLM.SampleRequests.Commands.FillProductColourNames;

namespace HRM.Application.Abstractions.Documents;

public interface IProductColourNamePreviewExcelRenderer
{
    byte[] Render(FillProductColourNamesResult preview);
}
