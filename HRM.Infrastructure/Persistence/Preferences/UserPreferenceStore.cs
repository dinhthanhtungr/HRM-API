using HRM.Application.Features.UserPreferences.Services;
using HRM.Domain.Entities.Preferences;
using HRM.Domain.Enums.Preferences;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HRM.Infrastructure.Persistence.Preferences;

public sealed class UserPreferenceStore(ApplicationDbContext db) : IUserPreferenceStore
{
    public async Task<IReadOnlyList<UserPreference>> ReadAsync(Guid userId,
        UserPreferenceCategory[] categories, CancellationToken cancellationToken)
    {
        try
        {
            return await db.UserPreferences.AsNoTracking()
                .Where(x => x.UserId == userId && categories.Contains(x.Category))
                .ToListAsync(cancellationToken);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            throw new PreferenceStorageUnavailableException();
        }
    }

    public async Task<bool> SaveAsync(UserPreference preference, long expectedRevision,
        CancellationToken cancellationToken)
    {
        try
        {
            if (expectedRevision == 0)
            {
                db.UserPreferences.Add(preference);
                try
                {
                    await db.SaveChangesAsync(cancellationToken);
                    return true;
                }
                catch (DbUpdateException exception) when
                    (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
                {
                    return false;
                }
                finally
                {
                    db.Entry(preference).State = EntityState.Detached;
                }
            }
            // Compare and write in one SQL statement: a stale reader cannot overwrite another device.
            return await db.UserPreferences
                .Where(x => x.UserId == preference.UserId && x.Category == preference.Category &&
                    x.Revision == expectedRevision)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Data, preference.Data)
                    .SetProperty(x => x.SchemaVersion, preference.SchemaVersion)
                    .SetProperty(x => x.Revision, preference.Revision)
                    .SetProperty(x => x.UpdatedAtUtc, preference.UpdatedAtUtc), cancellationToken) == 1;
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            throw new PreferenceStorageUnavailableException();
        }
        catch (DbUpdateException exception) when
            (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UndefinedTable })
        {
            throw new PreferenceStorageUnavailableException();
        }
    }
}
