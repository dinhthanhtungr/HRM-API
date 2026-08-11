using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.OrderSchema;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs
{
    public partial class ApplicationDbContext
    {
        public virtual DbSet<MerchandiseOrder> MerchandiseOrders { get; set; } = default!;
        public virtual DbSet<MerchandiseOrderDetail> MerchandiseOrderDetails { get; set; } = default!;
        public virtual DbSet<ComplaintReport> ComplaintReports { get; set; } = default!;
        public virtual DbSet<ComplaintReportLine> ComplaintReportLines { get; set; } = default!;
        public virtual DbSet<ComplaintReportLineLot> ComplaintReportLineLots { get; set; } = default!;
        public virtual DbSet<ComplaintCapaAction> ComplaintCapaActions { get; set; } = default!;
        public virtual DbSet<ComplaintReportApproval> ComplaintReportApprovals { get; set; } = default!;
        public virtual DbSet<PurchaseOrder> PurchaseOrders { get; set; } = default!;
        public virtual DbSet<PurchaseOrderDetail> PurchaseOrderDetails { get; set; } = default!;
        public virtual DbSet<PurchaseOrderSnapshot> PurchaseOrderSnapshots { get; set; } = default!;
        public virtual DbSet<PurchaseOrderLink> PurchaseOrderLinks { get; set; } = default!;
    }
}
