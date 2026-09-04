using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Application.Features.PLM.ColorChipRecords.Dtos;

namespace HRM.Infrastructure.Documents.Pdfs.ColorChipRecords
{
    public interface IColorChipRecordPortraitPdf
    {
        byte[] RenderTemplate();
        byte[] Render(ColorChipRecordPdfModel model, bool templateOnly = false);
    }
}
