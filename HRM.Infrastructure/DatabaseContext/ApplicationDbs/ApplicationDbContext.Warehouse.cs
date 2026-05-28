using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.WarehouseSchema;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs
{
    public partial class ApplicationDbContext
    {
        public virtual DbSet<WarehouseShelfStock> WarehouseShelfStocks { get; set; } = default!;
        public virtual DbSet<WarehouseTempStock> WarehouseTempStocks { get; set; } = default!;
        public virtual DbSet<WarehouseRequest> WarehouseRequests { get; set; } = default!;
        public virtual DbSet<WarehouseRequestDetail> WarehouseRequestDetails { get; set; } = default!;
        public virtual DbSet<WarehouseShelves> WarehouseShelves { get; set; } = default!;
        public virtual DbSet<WarehouseShelfLedger> WarehouseShelfLedgers { get; set; } = default!;
        public virtual DbSet<UsagePurpose> UsagePurposes { get; set; } = default!;
        public virtual DbSet<WarehouseVoucher> WarehouseVouchers { get; set; } = default!;
        public virtual DbSet<WarehouseVoucherDetail> WarehouseVoucherDetails { get; set; } = default!;

    }
}
