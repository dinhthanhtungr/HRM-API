using System.Text.Json;
using System.Text.Json.Serialization;
using HRM.Domain.Enums.Preferences;

namespace HRM.Application.Features.UserPreferences.Dtos;

public sealed record UserPreferenceDto(
    [property: JsonConverter(typeof(UserPreferenceCategoryConverter))] UserPreferenceCategory Category,
    int SchemaVersion,
    JsonElement Data, long Revision, DateTime? UpdatedAtUtc);

public sealed record SaveUserPreferenceRequest(int SchemaVersion,
    long ExpectedRevision, JsonElement Data);

public enum PreferenceError { None, Unauthorized, Invalid, Conflict, Unavailable }

public sealed record PreferenceResult<T>(T? Data, PreferenceError Error = PreferenceError.None)
{
    public static PreferenceResult<T> Fail(PreferenceError error) => new(default, error);
}

public sealed class UserPreferenceCategoryConverter : JsonStringEnumConverter<UserPreferenceCategory>
{
    public UserPreferenceCategoryConverter() : base(JsonNamingPolicy.CamelCase, allowIntegerValues: false) { }
}
