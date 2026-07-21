using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Entities.OrderSchema;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Abstractions.Persistence.Commons.Pricing
{
    public interface IPriceReadDbContext
    {
        DbSet<PurchaseOrderDetail> PurchaseOrderDetails { get; }
        DbSet<MaterialsSupplier> MaterialsSuppliers { get; }
        DbSet<MerchandiseOrderDetail> MerchandiseOrderDetails { get; }
    }
}
