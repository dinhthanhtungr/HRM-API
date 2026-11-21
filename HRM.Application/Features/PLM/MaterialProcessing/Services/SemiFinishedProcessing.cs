using System.Security.Cryptography;
using System.Text;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Entities.MaterialSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.MaterialProcessing.Services;

/// <summary>Adapter giữ contract NVL nội bộ, dữ liệu thực lưu ở MFG/VA và Product/SampleRequest.</summary>
internal static class SemiFinishedProcessing
{
    public const string RequestType = HRM.Application.Features.PLM.Shared.Rules.SemiFinishedRequestRules.RequestType;

    public static IQueryable<MfgProductionOrder> Orders(IPLMDbContext db, Guid company)
        => db.MfgProductionOrders.Where(x => x.CompanyId == company && x.IsActive
            && x.Product.CompanyId == company && x.Product.IsActive
            && x.Product.SampleRequests.Any(s => s.IsActive && s.CompanyId == company && s.RequestType == RequestType));

    public static string Process(MfgProductionOrder order) => order.StepOfProduct switch
    {
        HRM.Domain.Enums.Manufacturings.StepOfProduct.BTS_GH => "B",
        HRM.Domain.Enums.Manufacturings.StepOfProduct.BN_GH => "N",
        HRM.Domain.Enums.Manufacturings.StepOfProduct.CT_GH => "L",
        _ => ""
    };

    public static async Task<ManufacturingFormula?> FormulaAsync(IMaterialProcessingDbContext db, Guid company, Guid orderId, CancellationToken ct)
    {
        var id = await db.ProductionSelectVersions.Where(x => x.CompanyId == company && x.MfgProductionOrderId == orderId && x.ValidTo == null
                && x.ManufacturingFormula != null && x.ManufacturingFormula.IsActive && x.ManufacturingFormula.CompanyId == company)
            .OrderByDescending(x => x.ValidFrom).ThenByDescending(x => x.ProductionSelectVersionId)
            .Select(x => x.ManufacturingFormulaId).FirstOrDefaultAsync(ct);
        return id is null ? null : await db.ManufacturingFormulas.Include(x => x.ManufacturingFormulaMaterials).ThenInclude(x => x.Material)
            .FirstOrDefaultAsync(x => x.ManufacturingFormulaId == id && x.CompanyId == company && x.IsActive, ct);
    }

    // Giữ expectedRevision dạng int của API cũ, nhưng dùng token từ audit MFG/VA, không thêm cột version.
    public static int Revision(MfgProductionOrder order, ManufacturingFormula formula)
    {
        var content = $"{order.MfgProductionOrderId:D}|{order.UpdatedDate:yyyyMMddHHmmssffffff}|{order.Status}|{formula.UpdatedDate:yyyyMMddHHmmssffffff}|{formula.ManufacturingFormulaId:D}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return (BitConverter.ToInt32(hash, 0) & int.MaxValue) is var value && value > 0 ? value : 1;
    }

    public static MaterialProcessingFormula Adapt(MfgProductionOrder order, ManufacturingFormula formula)
    {
        var quantity = order.TotalQuantity ?? order.TotalQuantityRequest;
        var product = order.Product;
        return new MaterialProcessingFormula
        {
            MaterialProcessingFormulaId = order.MfgProductionOrderId, ExternalId = order.ExternalId,
            ProcessingType = Process(order), MaterialId = product.ProductId,
            Material = new Material { MaterialId = product.ProductId, ExternalId = product.ColourCode, Name = product.Name,
                CategoryId = product.CategoryId, Unit = product.Unit, CompanyId = order.CompanyId, IsActive = product.IsActive },
            TotalQuantityRequest = quantity, NumOfBatches = order.NumOfBatches, StepOfProduct = order.StepOfProduct,
            ManufacturingDate = order.ManufacturingDate, Status = order.Status, Note = order.LabNote, QcCheck = order.QcCheck,
            Revision = Revision(order, formula), CompanyId = order.CompanyId,
            CreatedDate = order.CreatedDate, UpdatedDate = order.UpdatedDate, CreatedBy = order.CreatedBy, UpdatedBy = order.UpdatedBy,
            Details = formula.ManufacturingFormulaMaterials.Where(x => x.IsActive).OrderBy(x => x.LineNo).Select(x => new MaterialProcessingFormulaDetail
            {
                MaterialProcessingFormulaDetailId = x.ManufacturingFormulaMaterialId, MaterialProcessingFormulaId = order.MfgProductionOrderId,
                MaterialId = x.MaterialId ?? Guid.Empty, Material = x.Material, LineNo = x.LineNo,
                MaterialCodeSnapshot = x.MaterialExternalIdSnapshot ?? "", MaterialNameSnapshot = x.MaterialNameSnapshot ?? "",
                QuantityKg = decimal.Round(x.Quantity * quantity, 3), LotNo = x.LotNo, itemType = x.itemType
            }).ToList()
        };
    }
}
