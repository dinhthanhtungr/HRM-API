using HRM.Domain.Entities.SampleRequestSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs;

public partial class ApplicationDbContext
{
    public DbSet<SampleRequestUserPreference> SampleRequestUserPreferences => Set<SampleRequestUserPreference>();
}
