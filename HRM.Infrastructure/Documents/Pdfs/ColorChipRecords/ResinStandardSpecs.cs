using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Enums.SampleRequests;

namespace HRM.Infrastructure.Documents.Pdfs.ColorChipRecords
{
    public static class ResinStandardSpecs
    {
        public static readonly IReadOnlyDictionary<ResinType, ResinStandardSpec> All
            = new Dictionary<ResinType, ResinStandardSpec>
            {
                [ResinType.PP] = new ResinStandardSpec
                {
                    ResinType = ResinType.PP,
                    SizeText = "2.4-3.3 ± 10%",
                    DiameterMin = 2.4m,
                    DiameterMax = 3.3m,
                    LengthMin = 2.4m,
                    LengthMax = 3.3m,
                    TolerancePercent = 10m,
                    PelletWeightMinGram = 40m,
                    PelletWeightMaxGram = 80m,
                    AntiStaticType = AntiStaticType.None
                },
                [ResinType.PE] = new ResinStandardSpec
                {
                    ResinType = ResinType.PE,
                    SizeText = "2.4-3.3 ± 10%",
                    DiameterMin = 2.4m,
                    DiameterMax = 3.3m,
                    LengthMin = 2.4m,
                    LengthMax = 3.3m,
                    TolerancePercent = 10m,
                    PelletWeightMinGram = 40m,
                    PelletWeightMaxGram = 80m,
                    AntiStaticType = AntiStaticType.None
                },
                [ResinType.ABS] = new ResinStandardSpec
                {
                    ResinType = ResinType.ABS,
                    SizeText = "2.4-3.3 ± 10%",
                    DiameterMin = 2.4m,
                    DiameterMax = 3.3m,
                    LengthMin = 2.4m,
                    LengthMax = 3.3m,
                    TolerancePercent = 10m,
                    PelletWeightMinGram = 50m,
                    PelletWeightMaxGram = 100m,
                    AntiStaticType = AntiStaticType.None
                },
                [ResinType.SAN] = new ResinStandardSpec
                {
                    ResinType = ResinType.SAN,
                    SizeText = "2.4-3.3 ± 10%",
                    DiameterMin = 2.4m,
                    DiameterMax = 3.3m,
                    LengthMin = 2.4m,
                    LengthMax = 3.3m,
                    TolerancePercent = 10m,
                    PelletWeightMinGram = 50m,
                    PelletWeightMaxGram = 100m,
                    AntiStaticType = AntiStaticType.None
                },
                [ResinType.Nylon] = new ResinStandardSpec
                {
                    ResinType = ResinType.Nylon,
                    SizeText = "2.4-3.3 ± 10%",
                    DiameterMin = 2.4m,
                    DiameterMax = 3.3m,
                    LengthMin = 2.4m,
                    LengthMax = 3.3m,
                    TolerancePercent = 10m,
                    PelletWeightMinGram = 40m,
                    PelletWeightMaxGram = 80m,
                    AntiStaticType = AntiStaticType.None
                },
                [ResinType.Tritan] = new ResinStandardSpec
                {
                    ResinType = ResinType.Tritan,
                    SizeText = "2.4-3.3 ± 10%",
                    DiameterMin = 2.4m,
                    DiameterMax = 3.3m,
                    LengthMin = 2.4m,
                    LengthMax = 3.3m,
                    TolerancePercent = 10m,
                    PelletWeightMinGram = 50m,
                    PelletWeightMaxGram = 100m,
                    AntiStaticType = AntiStaticType.None
                },
                [ResinType.PC] = new ResinStandardSpec
                {
                    ResinType = ResinType.PC,
                    SizeText = "2.4-3.3 ± 10%",
                    DiameterMin = 2.4m,
                    DiameterMax = 3.3m,
                    LengthMin = 2.4m,
                    LengthMax = 3.3m,
                    TolerancePercent = 10m,
                    PelletWeightMinGram = 50m,
                    PelletWeightMaxGram = 100m,
                    AntiStaticType = AntiStaticType.None
                },
                [ResinType.HDPE] = new ResinStandardSpec
                {
                    ResinType = ResinType.HDPE,
                    SizeText = "2.4-3.3 ± 10%",
                    DiameterMin = 2.4m,
                    DiameterMax = 3.3m,
                    LengthMin = 2.4m,
                    LengthMax = 3.3m,
                    TolerancePercent = 10m,
                    PelletWeightMinGram = 50m,
                    PelletWeightMaxGram = 101m,
                    AntiStaticType = AntiStaticType.None
                },
                [ResinType.PET] = new ResinStandardSpec
                {
                    ResinType = ResinType.PET,
                    SizeText = "1.8-3.5 ± 10%",
                    DiameterMin = 1.8m,
                    DiameterMax = 3.5m,
                    LengthMin = 1.8m,
                    LengthMax = 3.5m,
                    TolerancePercent = 10m,
                    PelletWeightMinGram = 70m,
                    PelletWeightMaxGram = 120m,
                    AntiStaticType = AntiStaticType.None
                },
                [ResinType.Other] = new ResinStandardSpec
                {
                    ResinType = ResinType.Other,
                    SizeText = "2.4-3.3 ± 10%",
                    DiameterMin = 2.4m,
                    DiameterMax = 3.3m,
                    LengthMin = 2.4m,
                    LengthMax = 3.3m,
                    TolerancePercent = 10m,
                    PelletWeightMinGram = 50m,
                    PelletWeightMaxGram = 100m,
                    AntiStaticType = AntiStaticType.None
                }
            };
    }
}
