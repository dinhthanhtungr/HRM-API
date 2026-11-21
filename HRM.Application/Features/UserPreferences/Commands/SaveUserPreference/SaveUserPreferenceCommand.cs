using HRM.Application.Features.UserPreferences.Dtos;
using MediatR;

namespace HRM.Application.Features.UserPreferences.Commands.SaveUserPreference;

public sealed record SaveUserPreferenceCommand(string Category, SaveUserPreferenceRequest Request)
    : IRequest<PreferenceResult<UserPreferenceDto>>;
