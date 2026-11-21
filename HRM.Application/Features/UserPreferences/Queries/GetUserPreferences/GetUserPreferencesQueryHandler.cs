using HRM.Application.Abstractions.Security;
using HRM.Application.Features.UserPreferences.Dtos;
using HRM.Application.Features.UserPreferences.Services;
using HRM.Domain.Enums.Preferences;
using MediatR;

namespace HRM.Application.Features.UserPreferences.Queries.GetUserPreferences;

public sealed class GetUserPreferencesQueryHandler(ICurrentUser user, IUserPreferenceStore store)
    : IRequestHandler<GetUserPreferencesQuery, PreferenceResult<IReadOnlyList<UserPreferenceDto>>>
{
    public async Task<PreferenceResult<IReadOnlyList<UserPreferenceDto>>> Handle(
        GetUserPreferencesQuery request, CancellationToken cancellationToken)
    {
        if (!user.IsAuthenticated || user.UserId == Guid.Empty)
            return PreferenceResult<IReadOnlyList<UserPreferenceDto>>.Fail(PreferenceError.Unauthorized);
        if (request.Categories.Length is 0 or > AppearancePreferenceRules.MaxCategories ||
            request.Categories.Any(name => !AppearancePreferenceRules.TryCategory(name, out _)))
            return PreferenceResult<IReadOnlyList<UserPreferenceDto>>.Fail(PreferenceError.Invalid);
        try
        {
            // Account-wide personal data: employee/company/acting preview are not the ownership key.
            var categories = request.Categories.Select(name =>
            {
                AppearancePreferenceRules.TryCategory(name, out var category);
                return category;
            }).Distinct().ToArray();
            var rows = await store.ReadAsync(user.UserId, categories, cancellationToken);
            try { return new(categories.Select(category => AppearancePreferenceRules.ToDto(
                rows.SingleOrDefault(row => row.Category == category), category)).ToArray()); }
            finally { foreach (var row in rows) row.Data.Dispose(); }
        }
        catch (PreferenceStorageUnavailableException)
        {
            return PreferenceResult<IReadOnlyList<UserPreferenceDto>>.Fail(PreferenceError.Unavailable);
        }
    }
}
