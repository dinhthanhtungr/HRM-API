using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Enums.SampleRequests;

namespace HRM.Infrastructure.Documents.Pdfs.ColorChipRecords
{
    public static class ResinStandardSpecHelper
    {
        public static ResinStandardSpec GetByResinType(ResinType resinType)
        {
            if (ResinStandardSpecs.All.TryGetValue(resinType, out var spec))
                return spec;

            return ResinStandardSpecs.All[ResinType.Other];
        }
    }
}
