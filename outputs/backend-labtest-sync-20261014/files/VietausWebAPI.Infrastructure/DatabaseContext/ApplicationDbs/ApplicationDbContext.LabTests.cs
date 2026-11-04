using VietausWebAPI.Core.Domain.Entities.DevandqaSchema;
using Microsoft.EntityFrameworkCore;

namespace VietausWebAPI.Infrastructure.DatabaseContext.ApplicationDbs;

public partial class ApplicationDbContext
{
    public DbSet<LabTest> LabTests { get; set; } = default!;
    public DbSet<LabTestResult> LabTestResults { get; set; } = default!;
}
