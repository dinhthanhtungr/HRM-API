using HRM.Application.Features.UserPreferences.Dtos;
using MediatR;

namespace HRM.Application.Features.UserPreferences.Queries.GetUserPreference;

public sealed record GetUserPreferenceQuery(string Category) : IRequest<PreferenceResult<UserPreferenceDto>>;
