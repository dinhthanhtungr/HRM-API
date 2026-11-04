using HRM.Application.Features.PLM.ProductionOrders.Dtos;
using HRM.Application.Features.PLM.ProductionOrders.Rules;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Enums.Manufacturings;

namespace HRM.Application.Features.PLM.ProductionOrders.Services;

internal static class ProductionOrderInformFactory
{
    public static MfgProductionOrder Create(
        CreateProductionOrderInformRequest request, Guid companyId, Guid employeeId, DateTime now, string code)
        => new()
        {
            MfgProductionOrderId = Guid.CreateVersion7(), ExternalId = code,
            ProductId = request.ProductId!.Value, ProductExternalIdSnapshot = request.ProductExternalIdSnapshot,
            ProductNameSnapshot = request.ProductNameSnapshot,
            CustomerId = request.CustomerId, CustomerExternalIdSnapshot = request.CustomerExternalIdSnapshot,
            CustomerNameSnapshot = request.CustomerNameSnapshot,
            FormulaId = request.FormulaCustomerSelect == Guid.Empty ? null : request.FormulaCustomerSelect,
            FormulaExternalIdSnapshot = request.FormulaCustomerExternalIdSelect,
            ManufacturingDate = request.ManufacturingDate, ExpectedDate = request.ExpectedDate,
            RequiredDate = request.RequiredDate, TotalQuantityRequest = request.TotalQuantityRequest,
            TotalQuantity = request.TotalQuantity, NumOfBatches = request.NumOfBatches,
            UnitPriceAgreed = request.UnitPriceAgreed,
            Status = ProductionOrderCreationRules.ActiveItems(request).Count > 0
                ? ManufacturingProductOrder.Scheduling.ToString() : ManufacturingProductOrder.New.ToString(),
            LabNote = request.LabNote, Requirement = request.Requirement, PlpuNote = request.PlpuNote,
            QcCheck = request.QcCheck, BagType = request.BagType ?? string.Empty, StepOfProduct = request.StepOfProduct,
            IsActive = true, CompanyId = companyId, CreatedDate = now, UpdatedDate = now,
            CreatedBy = employeeId, UpdatedBy = employeeId
        };
}
