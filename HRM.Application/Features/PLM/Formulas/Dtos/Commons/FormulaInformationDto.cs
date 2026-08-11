using HRM.Application.Commons.Pricing.Dtos;
using HRM.Domain.Enums.Products;

namespace HRM.Application.Features.PLM.Formulas.Dtos.Commons
{
    public class FormulaInformationDto
    {
        public Guid FormulaId { get; set; }

        public string ExternalId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        public string Status { get; set; } = FormulaStatus.Draft.ToString();

        public Guid? CheckBy { get; set; }
        public string? CheckByName { get; set; }
        public DateTime? CheckDate { get; set; }

        public Guid? SentBy { get; set; }
        public string? SentByName { get; set; }
        public DateTime? SentDate { get; set; }

        public decimal? TotalPrice { get; set; }
        public decimal? RealtimeMaterialCost { get; set; }
        public bool? IsRealtimeMaterialCostComplete { get; set; }
        public int? MissingMaterialPriceCount { get; set; }

        public DateTime? EffectiveDate { get; set; }
        public decimal? ProductionPrice { get; set; }
        public decimal? PresidentPrice { get; set; }
        public decimal? ProfitMarginPrice { get; set; }
        public decimal? ManufacturingCost { get; set; }
        public decimal? StandardSellingPrice { get; set; }
        public decimal? ProfitMarginRate { get; set; }
        public FormulaPriceCalculationDto? Pricing { get; set; }

        public bool IsSelect { get; set; }
        public bool IsActive { get; set; }

        public string? Note { get; set; }

        public DateTime? CreatedDate { get; set; }

        public IReadOnlyList<FormulaMaterialInformationDto> Materials { get; set; }
            = new List<FormulaMaterialInformationDto>();
    }
}
