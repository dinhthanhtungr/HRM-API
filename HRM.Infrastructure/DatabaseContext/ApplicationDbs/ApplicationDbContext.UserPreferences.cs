using HRM.Domain.Entities.Preferences;
using Microsoft.EntityFrameworkCore;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs;

public partial class ApplicationDbContext
{
    public DbSet<UserPreference> UserPreferences => Set<UserPreference>();
}
