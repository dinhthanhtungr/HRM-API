using HRM.Application.Abstractions.Persistence.Reports;
using Microsoft.EntityFrameworkCore;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs;

public partial class ApplicationDbContext
{
    public virtual DbSet<ElectricityMonthlyBillRow> ElectricityMonthlyBillRows { get; set; } = default!;
}
