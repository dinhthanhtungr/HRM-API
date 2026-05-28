using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.PrintectSchema;
using HRM.Domain.Entities.StandardParameterSchema;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs
{
    public partial class ApplicationDbContext
    {
        public virtual DbSet<ParameterStandard> ParameterStandards { get; set; } = default!;
        public virtual DbSet<MachineProductivity> MachineProductivitys { get; set; } = default!;

    }
}
