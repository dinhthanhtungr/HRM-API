using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.SupplyRequestSchema;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs
{
    public partial class ApplicationDbContext
    {
        public virtual DbSet<SupplyRequest> SupplyRequests { get; set; } = default!;
        public virtual DbSet<SupplyRequestDetail> SupplyRequestDetails { get; set; } = default!;
    }
}
