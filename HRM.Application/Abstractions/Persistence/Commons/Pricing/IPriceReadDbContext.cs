using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Entities.SampleRequestSchema;
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
        DbSet<ProductPricingVersion> ProductPricingVersions { get; }
        DbSet<Formula> Formulas { get; }
        DbSet<FormulaMaterial> FormulaMaterials { get; }
    }
}
