using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities;
using HRM.Domain.Entities.MaterialSchema;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs
{
    public partial class ApplicationDbContext
    {
        public virtual DbSet<Material> Materials { get; set; } = default!;
        public virtual DbSet<MaterialGroupName> MaterialGroupNames { get; set; } = default!;
        public virtual DbSet<MaterialsSupplier> MaterialsSuppliers { get; set; } = default!;
        public virtual DbSet<PriceHistory> PriceHistories { get; set; } = default!;
        public virtual DbSet<Supplier> Suppliers { get; set; } = default!;
        public virtual DbSet<SupplierAddress> SupplierAddresses { get; set; } = default!;
        public virtual DbSet<SupplierContact> SupplierContacts { get; set; } = default!;
        public virtual DbSet<Category> Categories { get; set; } = default!;
        public virtual DbSet<Unit> Units { get; set; } = default!;
    }
}
