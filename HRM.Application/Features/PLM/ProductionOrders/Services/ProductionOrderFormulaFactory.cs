using HRM.Application.Features.PLM.ProductionOrders.Dtos;
using HRM.Application.Features.PLM.ProductionOrders.Rules;
using HRM.Domain.Entities;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Enums.Formulas;
using HRM.Domain.Enums.Manufacturings;

namespace HRM.Application.Features.PLM.ProductionOrders.Services;

internal static class ProductionOrderFormulaFactory
{
    public static ManufacturingFormula Create(
        CreateProductionOrderInformRequest request, Guid companyId, Guid employeeId, DateTime now, string code)
    {
        var items = ProductionOrderCreationRules.ActiveItems(request);
        var formula = new ManufacturingFormula
        {
            ManufacturingFormulaId = Guid.CreateVersion7(), ExternalId = code, Name = "COPY",
            Status = ManufacturingProductOrderFormula.Checking.ToString(),
            TotalPrice = items.Sum(x => x.UnitPrice * x.Quantity),
            SourceType = FormulaSource.FromVA,
            SourceManufacturingFormulaId = request.ManufacturingFormulaIdIsSelect,
            SourceManufacturingExternalIdSnapshot = request.ManufacturingFormulaExternalIdIsSelect,
            SourceVUFormulaId = request.FormulaCustomerSelect == Guid.Empty ? null : request.FormulaCustomerSelect,
            SourceVUExternalIdSnapshot = request.FormulaCustomerExternalIdSelect,
            IsActive = true, Note = request.LabNote, CompanyId = companyId,
            CreatedDate = now, UpdatedDate = now, CreatedBy = employeeId, UpdatedBy = employeeId
        };
        formula.ManufacturingFormulaMaterials = items.Select((item, index) => new ManufacturingFormulaMaterial
        {
            ManufacturingFormulaMaterialId = Guid.CreateVersion7(),
            ManufacturingFormulaId = formula.ManufacturingFormulaId,
            itemType = ProductionOrderCreationRules.ResolveItemType(item.ItemType, item.LotNumber),
            MaterialId = ProductionOrderCreationRules.IsMaterial(item.ItemType) ? item.ItemId : null,
            ProductId = ProductionOrderCreationRules.IsMaterial(item.ItemType) ? null : item.ItemId,
            CategoryId = item.CategoryId, LineNo = index + 1,
            Quantity = item.Quantity, UnitPrice = item.UnitPrice, TotalPrice = item.UnitPrice * item.Quantity,
            MaterialNameSnapshot = item.MaterialNameSnapshot,
            MaterialExternalIdSnapshot = item.MaterialExternalIdSnapshot,
            LotNo = ProductionOrderCreationRules.NormalizeLotNumber(item.LotNumber),
            Unit = item.Unit, IsActive = item.IsActive
        }).ToList();
        return formula;
    }

    public static ProductionSelectVersion CreateSelection(MfgProductionOrder order, ManufacturingFormula formula, DateTime now)
        => new()
        {
            ProductionSelectVersionId = Guid.CreateVersion7(), MfgProductionOrderId = order.MfgProductionOrderId,
            ManufacturingFormulaId = formula.ManufacturingFormulaId, ValidFrom = now, ValidTo = null,
            CreatedBy = order.CreatedBy, ClosedBy = null, CompanyId = order.CompanyId
        };

    public static SchedualMfg CreateSchedule(MfgProductionOrder order, DateTime now)
        => new()
        {
            MfgProductionOrderId = order.MfgProductionOrderId, ProductId = order.ProductId,
            requirement = order.Requirement, Note = order.LabNote ?? order.PlpuNote,
            DeliveryPlanDate = order.ExpectedDate, CreatedDate = now,
            Status = ManufacturingProductOrder.Scheduling.ToString(), StepOfProduct = order.StepOfProduct
        };
}
