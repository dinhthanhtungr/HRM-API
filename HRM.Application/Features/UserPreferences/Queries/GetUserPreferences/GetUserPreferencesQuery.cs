using HRM.Application.Features.UserPreferences.Dtos;
using MediatR;

namespace HRM.Application.Features.UserPreferences.Queries.GetUserPreferences;

public sealed record GetUserPreferencesQuery(string[] Categories)
    : IRequest<PreferenceResult<IReadOnlyList<UserPreferenceDto>>>;
