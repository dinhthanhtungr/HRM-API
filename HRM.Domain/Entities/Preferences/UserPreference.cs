using System.Text.Json;
using HRM.Domain.Enums.Preferences;
using HRM.Domain.Identity;

namespace HRM.Domain.Entities.Preferences;

public sealed class UserPreference
{
    public Guid UserId { get; set; }
    public UserPreferenceCategory Category { get; set; }
    public int SchemaVersion { get; set; } = 1;
    public JsonDocument Data { get; set; } = null!;
    public long Revision { get; set; } = 1;
    public DateTime UpdatedAtUtc { get; set; }
    public ApplicationUser User { get; set; } = null!;
}
