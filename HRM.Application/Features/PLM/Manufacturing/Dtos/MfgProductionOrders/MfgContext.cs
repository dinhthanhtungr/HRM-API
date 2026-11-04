using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulas;

namespace HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders
{
    public class MfgContext
    {
        public Dictionary<Guid, ProductRow> Products { get; init; } = new();
        // VA chuẩn hiện hành theo Product
        public Dictionary<Guid, (Guid VaId, string VaCode, Guid? SourceVuId, string? SourceVuCode)> StandardVaByProduct { get; init; } = new();
        public Dictionary<Guid, List<FmItemRow>> FmItemsByVu { get; init; } = new();
        public Dictionary<Guid, List<FmItemRow>> FmItemsByVa { get; init; } = new();
        public Dictionary<Guid, decimal> PriceMap { get; init; } = new();
        // VU đã từng có VA chưa?
        public Dictionary<Guid, bool> VuHasAnyVa { get; init; } = new();
    }
}
