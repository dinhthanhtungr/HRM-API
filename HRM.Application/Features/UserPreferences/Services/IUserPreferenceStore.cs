using HRM.Domain.Entities.Preferences;
using HRM.Domain.Enums.Preferences;

namespace HRM.Application.Features.UserPreferences.Services;

public interface IUserPreferenceStore
{
    Task<IReadOnlyList<UserPreference>> ReadAsync(Guid userId,
        UserPreferenceCategory[] categories, CancellationToken cancellationToken);
    Task<bool> SaveAsync(UserPreference preference, long expectedRevision,
        CancellationToken cancellationToken);
}

public sealed class PreferenceStorageUnavailableException : Exception;

