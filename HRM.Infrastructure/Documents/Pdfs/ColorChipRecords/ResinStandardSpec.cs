using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Enums.SampleRequests;

namespace HRM.Infrastructure.Documents.Pdfs.ColorChipRecords
{
    public class ResinStandardSpec
    {
        public ResinType ResinType { get; set; }

        public string SizeText { get; set; } = string.Empty;
        public decimal? DiameterMin { get; set; }
        public decimal? DiameterMax { get; set; }
        public decimal? LengthMin { get; set; }
        public decimal? LengthMax { get; set; }
        public decimal? TolerancePercent { get; set; }

        public decimal? PelletWeightMinGram { get; set; }
        public decimal? PelletWeightMaxGram { get; set; }

        public AntiStaticType AntiStaticType { get; set; }
    }
}
