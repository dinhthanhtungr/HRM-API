using HRM.Application.Abstractions.Persistence.Purchasing;
using HRM.Domain.Enums.Devandqa;
using HRM.Domain.Enums.WareHouses;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Reports.PLPU
{
    public partial class PLPUPurchaseOverviewReportRepository : IPLPUPurchaseOverviewReportRepository
    {
        private const string SupplierOrderType = "NCC";

        private const string IncludedMaterialCodePrefix = "NVL";

        private static readonly string[] ExcludedMaterialCodeKeywords =
        {
            "VPP",
            "BB"
        };

        private static readonly int[] PurchaseReceiptVoucherTypes =
        {
            (int)WareHouseRequestType.ImportRawMaterial,
            (int)WareHouseRequestType.ImportOther,
            (int)WareHouseRequestType.ImportMaterial
        };

        private readonly IPurchaseOrderDbContext _context;

        public PLPUPurchaseOverviewReportRepository(IPurchaseOrderDbContext context)
        {
            _context = context;
        }

        public async Task<PLPUPurchaseReportRawData> GetRawDataAsync(
            PLPUPurchaseOverviewQuery query,
            CancellationToken ct)
        {
            query ??= new PLPUPurchaseOverviewQuery();

            if (!query.CompanyId.HasValue || query.CompanyId == Guid.Empty)
                throw new InvalidOperationException("A current company is required for PLPU reports.");

            var orderLines = await BuildOrderLineQuery(query).ToListAsync(ct);

            var rawReceipts = await BuildReceiptQuery(query).ToListAsync(ct);

            var receipts = NormalizeReceiptRows(rawReceipts)
                .Select(ApplyReceiptQuantities)
                .ToList();

            var qcRows = receipts
                .Select(ToQcRow)
                .Select(ApplyQcGroup)
                .ToList();

            return new PLPUPurchaseReportRawData
            {
                OrderLines = orderLines,
                Receipts = receipts,
                QcRows = qcRows
            };
        }

    }
}
