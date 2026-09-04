using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.PLM.ColorChipRecords.Dtos
{
    public class ColorChipRecordPdfModel
    {
        public string BatchNo { get; set; } = "SAMPLE";
        public DateTime? Date { get; set; }

        public string Customer { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string AddRate { get; set; } = string.Empty;
        public string Resin { get; set; } = string.Empty;

        public string ApprovalText { get; set; }
            = "PLEASE RETURN ONE/TWO SETS TO VIETAUS POLYMER UPON APPROVAL";

        public string PreparedBy { get; set; } = string.Empty;
        public string Signature { get; set; } = string.Empty;

        public string Title { get; set; } = "COLOR CHIPS";
        public string LowerText { get; set; } = "LOWER";
        public string StandardText { get; set; } = "STANDARD";
        public string UpperText { get; set; } = "UPPER";

        public string? Machine { get; set; }
        public string? TemperatureLimit { get; set; }
        public string? SizeText { get; set; }
        public decimal? PelletWeightGram { get; set; }
        public string? NetWeightGram { get; set; }
        public bool? Electrostatic { get; set; }
        public string? Note { get; set; }
        public string? PrintNote { get; set; }
        public string? DeltaE { get; set; }

        public string RecordTypeText { get; set; } = string.Empty;
        public string ResinTypeText { get; set; } = string.Empty;
        public string LogoTypeText { get; set; } = string.Empty;
        public string FormStyleText { get; set; } = string.Empty;

        public List<string> DevelopmentFormulaCodes { get; set; } = new();
    }

    public sealed class ColorChipRecordPdfFileDto
    {
        public string FileName { get; set; } = "ColorChipRecord.pdf";
        public string ContentType { get; set; } = "application/pdf";
        public byte[] Content { get; set; } = [];
    }
}
