using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.PrintectSchema;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs
{
    public partial class ApplicationDbContext
    {
        // ======= DbSet cho PrintcctSchema =======
        public virtual DbSet<HistoryPrintLabelForAll> HistoryPrintLabelForAlls { get; set; } = default!;
        public virtual DbSet<LabelElement> LabelElements { get; set; } = default!;
        public virtual DbSet<LabelTemplate> LabelTemplates { get; set; } = default!;
        public virtual DbSet<CustomerLabelHeader> CustomerLabelHeaders { get; set; } = default!;
        public virtual DbSet<CustomerLabelDetail> CustomerLabelDetails { get; set; } = default!;

    }
}
