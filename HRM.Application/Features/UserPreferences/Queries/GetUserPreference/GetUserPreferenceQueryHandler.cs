using HRM.Application.Abstractions.Security;
using HRM.Application.Features.UserPreferences.Dtos;
using HRM.Application.Features.UserPreferences.Services;
using MediatR;

namespace HRM.Application.Features.UserPreferences.Queries.GetUserPreference;

public sealed class GetUserPreferenceQueryHandler(ICurrentUser user, IUserPreferenceStore store)
    : IRequestHandler<GetUserPreferenceQuery, PreferenceResult<UserPreferenceDto>>
{
    public async Task<PreferenceResult<UserPreferenceDto>> Handle(
        GetUserPreferenceQuery request, CancellationToken cancellationToken)
    {
        if (!user.IsAuthenticated || user.UserId == Guid.Empty)
            return PreferenceResult<UserPreferenceDto>.Fail(PreferenceError.Unauthorized);
        if (!AppearancePreferenceRules.TryCategory(request.Category, out var category))
            return PreferenceResult<UserPreferenceDto>.Fail(PreferenceError.Invalid);
        try
        {
            var rows = await store.ReadAsync(user.UserId, [category], cancellationToken);
            try { return new(AppearancePreferenceRules.ToDto(rows.SingleOrDefault(), category)); }
            finally { foreach (var row in rows) row.Data.Dispose(); }
        }
        catch (PreferenceStorageUnavailableException)
        {
            return PreferenceResult<UserPreferenceDto>.Fail(PreferenceError.Unavailable);
        }
    }
}
