using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.DevandqaSchema;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs
{
    public partial class ApplicationDbContext
    {
        public virtual DbSet<ProductStandard> ProductStandards { get; set; } = default!;
        public virtual DbSet<ProductTest> ProductTests { get; set; } = default!;
        public virtual DbSet<QcPassDetailHistory> QcPassDetailHistories { get; set; } = default!;
        public virtual DbSet<QcPassHistory> QcPassHistories { get; set; } = default!;
        public virtual DbSet<ProductInspection> ProductInspections { get; set; } = default!;
        public virtual DbSet<QCInputByQC> QCInputByQCs { get; set; } = default!;
        //public virtual DbSet<QCInputByWarehouse> QCInputByWarehouses { get; set; } = default!;
    }
}
