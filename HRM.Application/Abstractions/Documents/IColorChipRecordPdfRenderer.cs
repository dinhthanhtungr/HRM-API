using HRM.Application.Features.PLM.ColorChipRecords.Dtos;
using HRM.Domain.Enums.SampleRequests;

namespace HRM.Application.Abstractions.Documents;

public interface IColorChipRecordPdfRenderer
{
    byte[] Render(ColorChipRecordPdfModel model, FormStyle formStyle);
}
