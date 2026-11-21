using System.Text.Json;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.UserPreferences.Dtos;
using HRM.Application.Features.UserPreferences.Services;
using HRM.Domain.Entities.Preferences;
using MediatR;

namespace HRM.Application.Features.UserPreferences.Commands.SaveUserPreference;

public sealed class SaveUserPreferenceCommandHandler(ICurrentUser user, IUserPreferenceStore store)
    : IRequestHandler<SaveUserPreferenceCommand, PreferenceResult<UserPreferenceDto>>
{
    public async Task<PreferenceResult<UserPreferenceDto>> Handle(
        SaveUserPreferenceCommand command, CancellationToken cancellationToken)
    {
        if (!user.IsAuthenticated || user.UserId == Guid.Empty)
            return PreferenceResult<UserPreferenceDto>.Fail(PreferenceError.Unauthorized);
        if (!AppearancePreferenceRules.TryCategory(command.Category, out var category) ||
            AppearancePreferenceRules.Normalize(command.Request, category) is not { } data)
            return PreferenceResult<UserPreferenceDto>.Fail(PreferenceError.Invalid);
        using var document = JsonDocument.Parse(data.GetRawText());
        var preference = new UserPreference
        {
            UserId = user.UserId, Category = category,
            SchemaVersion = command.Request.SchemaVersion, Data = document,
            Revision = command.Request.ExpectedRevision + 1, UpdatedAtUtc = DateTime.UtcNow
        };
        try
        {
            if (!await store.SaveAsync(preference, command.Request.ExpectedRevision, cancellationToken))
                return PreferenceResult<UserPreferenceDto>.Fail(PreferenceError.Conflict);
            return new(AppearancePreferenceRules.ToDto(preference));
        }
        catch (PreferenceStorageUnavailableException)
        {
            return PreferenceResult<UserPreferenceDto>.Fail(PreferenceError.Unavailable);
        }
    }
}
