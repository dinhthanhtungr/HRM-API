using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulas;
using HRM.Domain.Enums.Formulas;
using HRM.Domain.Enums.Manufacturings;

namespace HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrderRWs.UpsertInformationDtos
{
    public class PostMfgFormulaAndUpdateMpoRequest
    {
        // MPO
        public Guid MfgProductionOrderId { get; set; }
        public DateTime? ManufacturingDate { get; set; }
        public DateTime? ExpectedDate { get; set; }
        public DateTime? RequiredDate { get; set; }
        public decimal? TotalQuantity { get; set; }
        public int? NumOfBatches { get; set; }
        public string? LabNote { get; set; }
        public string? Requirement { get; set; }
        public string? PlpuNote { get; set; }
        public StepOfProduct? StepOfProduct { get; set; }
        public string? QcCheck { get; set; }
        // Formula
        public Guid ProductId { get; set; }
        public FormulaSource? SourceType { get; set; }
        public Guid? FormulaSourceId { get; set; }
        public string? SourceManufacturingExternalIdSnapshot { get; set; }
        public Guid? VUFormulaId { get; set; }
        public string? FormulaExternalIdSnapshot { get; set; }
        public FormulaType FormulaType { get; set; }
        public bool IsStandard { get; set; }
        public string? Note { get; set; }
        public string? FormulaStatus { get; set; }
        public List<PostManufacturingFormulaMaterial> ManufacturingFormulaMaterials { get; set; } = new();
    }
}
