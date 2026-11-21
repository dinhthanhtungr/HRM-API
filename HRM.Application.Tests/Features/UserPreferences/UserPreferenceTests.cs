using System.Text.Json;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.UserPreferences.Commands.SaveUserPreference;
using HRM.Application.Features.UserPreferences.Dtos;
using HRM.Application.Features.UserPreferences.Queries.GetUserPreferences;
using HRM.Application.Features.UserPreferences.Queries.GetUserPreference;
using HRM.Application.Features.UserPreferences.Services;
using HRM.Domain.Entities.Preferences;
using HRM.Domain.Enums.Preferences;
using HRM.Domain.Identity;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.UserPreferences;

public sealed class UserPreferenceTests
{
    private static JsonElement Data(string preset = "forest") =>
        JsonSerializer.SerializeToElement(new { preset, background = "neutral", emphasis = "light" });

    [Theory]
    [InlineData("{}", 1)]
    [InlineData("{\"preset\":\"custom\",\"color\":\"url(bad)\",\"background\":\"neutral\",\"emphasis\":\"light\"}", 1)]
    [InlineData("{\"preset\":\"forest\",\"background\":\"wrong\",\"emphasis\":\"light\"}", 1)]
    [InlineData("{\"preset\":\"forest\",\"background\":\"neutral\",\"emphasis\":\"light\",\"role\":\"admin\"}", 1)]
    [InlineData("{\"preset\":\"forest\",\"preset\":\"rose\",\"background\":\"neutral\",\"emphasis\":\"light\"}", 1)]
    [InlineData("{\"preset\":\"forest\",\"background\":\"neutral\",\"emphasis\":\"light\"}", 2)]
    public void RejectsInvalidData(string json, int version)
    {
        Assert.Null(AppearancePreferenceRules.Normalize(new(version, 0,
            JsonSerializer.Deserialize<JsonElement>(json))));
    }

    [Fact]
    public void CustomColorIsCanonicalAndUnknownCategoriesAreRejected()
    {
        var data = JsonSerializer.Deserialize<JsonElement>(
            "{\"preset\":\"custom\",\"color\":\"#AABBCC\",\"background\":\"tinted\",\"emphasis\":\"strong\"}");
        Assert.Equal("#aabbcc", AppearancePreferenceRules.Normalize(new(1, 0, data))!.Value.GetProperty("color").GetString());
        Assert.False(AppearancePreferenceRules.TryCategory("1", out _));
        Assert.False(AppearancePreferenceRules.TryCategory("crm.customers.list", out _));
        Assert.Null(AppearancePreferenceRules.Normalize(new(1, -1, Data())));
        Assert.Null(AppearancePreferenceRules.Normalize(new(1, long.MaxValue, Data())));
    }

    [Fact]
    public async Task MissingRowReturnsDefaultWithoutWritingOrRequiringEmployee()
    {
        var user = new User(Guid.NewGuid());
        var store = new Store();
        var result = await new GetUserPreferencesQueryHandler(user, store)
            .Handle(new(["appearance"]), default);
        Assert.Equal(PreferenceError.None, result.Error);
        Assert.Equal(0, Assert.Single(result.Data!).Revision);
        Assert.Equal("neutral", result.Data![0].Data.GetProperty("background").GetString());
        Assert.Equal(user.UserId, store.LastUser);
        Assert.Equal(0, store.Writes);
    }

    [Fact]
    public async Task SaveScopesByIdentityAndStaleRevisionConflicts()
    {
        var user = new User(Guid.NewGuid());
        var store = new Store();
        var handler = new SaveUserPreferenceCommandHandler(user, store);
        var first = await handler.Handle(new("appearance", new(1, 0, Data())), default);
        Assert.Equal(1, first.Data!.Revision);
        Assert.Equal(user.UserId, store.LastUser);
        var stale = await handler.Handle(new("appearance", new(1, 0, Data("rose"))), default);
        Assert.Equal(PreferenceError.Conflict, stale.Error);
        var other = await new GetUserPreferencesQueryHandler(new User(Guid.NewGuid()), store)
            .Handle(new(["appearance"]), default);
        Assert.Equal(0, other.Data![0].Revision);
        var next = await handler.Handle(new("appearance", new(1, 1, Data("rose"))), default);
        Assert.Equal(2, next.Data!.Revision);
    }

    [Fact]
    public async Task UnauthenticatedAndInvalidRequestsNeverReachStorage()
    {
        var store = new Store();
        var anonymous = new User(Guid.Empty);
        Assert.Equal(PreferenceError.Unauthorized,
            (await new SaveUserPreferenceCommandHandler(anonymous, store)
                .Handle(new("appearance", new(1, 0, Data())), default)).Error);
        Assert.Equal(PreferenceError.Unauthorized,
            (await new GetUserPreferencesQueryHandler(anonymous, store)
                .Handle(new(["appearance"]), default)).Error);
        var user = new User(Guid.NewGuid());
        Assert.Equal(PreferenceError.Invalid,
            (await new GetUserPreferencesQueryHandler(user, store)
                .Handle(new(["unknown"]), default)).Error);
        Assert.Equal(PreferenceError.Invalid,
            (await new SaveUserPreferenceCommandHandler(user, store)
                .Handle(new("1", new(1, 0, Data())), default)).Error);
        Assert.Equal(Guid.Empty, store.LastUser);
    }

    [Fact]
    public async Task MissingDeploymentIsReportedAsUnavailable()
    {
        var user = new User(Guid.NewGuid());
        var store = new Store { Unavailable = true };
        Assert.Equal(PreferenceError.Unavailable,
            (await new GetUserPreferencesQueryHandler(user, store).Handle(new(["appearance"]), default)).Error);
        Assert.Equal(PreferenceError.Unavailable,
            (await new SaveUserPreferenceCommandHandler(user, store)
                .Handle(new("appearance", new(1, 0, Data())), default)).Error);
    }

    [Fact]
    public void EfModelHasCompositeKeyIdentityForeignKeyAndNoRedundantIndexes()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var entity = db.Model.FindEntityType(typeof(UserPreference))!;
        Assert.Equal("public", entity.GetSchema());
        Assert.Equal("UserPreferences", entity.GetTableName());
        Assert.Equal(new[] { "UserId", "Category" }, entity.FindPrimaryKey()!.Properties.Select(p => p.Name));
        var fk = Assert.Single(entity.GetForeignKeys());
        Assert.Equal(typeof(ApplicationUser), fk.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Cascade, fk.DeleteBehavior);
        Assert.All(fk.Properties, p => Assert.False(p.IsShadowProperty()));
        Assert.Empty(entity.GetIndexes());
        Assert.Equal("jsonb", entity.FindProperty("Data")!.GetColumnType());
        Assert.Equal("timestamp with time zone", entity.FindProperty("UpdatedAtUtc")!.GetColumnType());
        Assert.True(entity.FindProperty("Revision")!.IsConcurrencyToken);
        var id = Guid.NewGuid();
        var sql = db.UserPreferences.Where(p => p.UserId == id &&
            p.Category == UserPreferenceCategory.Appearance).ToQueryString();
        Assert.Contains("\"UserId\"", sql);
        Assert.Contains("\"Category\"", sql);
    }

    [Fact]
    public async Task DetailUsesAuthenticatedAccountAndRejectsUnsupportedCategory()
    {
        var user = new User(Guid.NewGuid());
        var store = new Store();
        var handler = new GetUserPreferenceQueryHandler(user, store);
        Assert.Equal(PreferenceError.Invalid, (await handler.Handle(new("unknown"), default)).Error);
        Assert.Equal(Guid.Empty, store.LastUser);
        var result = await handler.Handle(new("appearance"), default);
        Assert.Equal(user.UserId, store.LastUser);
        Assert.Equal(UserPreferenceCategory.Appearance, result.Data!.Category);
        Assert.Equal(0, result.Data.Revision);
    }

    [Fact]
    public void ResponseCategoryUsesNamedEnumAndDataIsIndependentOfDocumentLifetime()
    {
        var dto = AppearancePreferenceRules.ToDto(null);
        var json = JsonSerializer.SerializeToElement(dto);
        Assert.Equal("appearance", json.GetProperty("Category").GetString());
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<UserPreferenceDto>(
            "{\"Category\":1,\"SchemaVersion\":1,\"Data\":{},\"Revision\":0}"));
        using var document = JsonDocument.Parse(Data().GetRawText());
        var stored = AppearancePreferenceRules.ToDto(new UserPreference
        {
            Category = UserPreferenceCategory.Appearance, Data = document
        });
        document.Dispose();
        Assert.Equal("forest", stored.Data.GetProperty("preset").GetString());
    }

    // Existing identity checks also apply to the newly supported categories.
    [Theory]
    [InlineData("display")]
    [InlineData("custom-theme-library")]
    public async Task AdvancedCategoryRejectsUnauthenticatedWrites(string route)
    {
        var user = new User(Guid.Empty);
        var store = new Store();
        AppearancePreferenceRules.TryCategory(route, out var category);
        var request = new SaveUserPreferenceRequest(1, 0, AdvancedPreferenceRules.DefaultData(category));
        var result = await new SaveUserPreferenceCommandHandler(user, store).Handle(new(route, request), default);
        Assert.Equal(PreferenceError.Unauthorized, result.Error);
        Assert.Equal(0, store.Writes);
    }

    [Fact]
    public async Task ReadManyDeduplicatesAndReturnsIndependentDefaultsWithoutWrites()
    {
        var user = new User(Guid.NewGuid());
        var store = new Store();
        var result = await new GetUserPreferencesQueryHandler(user, store)
            .Handle(new(["display", "appearance", "custom-theme-library", "display"]), default);
        Assert.Equal(PreferenceError.None, result.Error);
        Assert.Equal(3, result.Data!.Count);
        Assert.Equal(new[] {UserPreferenceCategory.Display, UserPreferenceCategory.Appearance, UserPreferenceCategory.CustomThemeLibrary}, result.Data.Select(row => row.Category));
        Assert.All(result.Data, row => Assert.Equal(0, row.Revision));
        Assert.Equal("light", result.Data[0].Data.GetProperty("colorScheme").GetString());
        Assert.Equal(0, result.Data[2].Data.GetProperty("samples").GetArrayLength());
        Assert.Equal(user.UserId, store.LastUser);
        Assert.Equal(0, store.Writes);
    }

    private sealed record User(Guid UserId) : ICurrentUser
    {
        public bool IsAuthenticated => UserId != Guid.Empty;
        public Guid? EmployeeId => null;
        public Guid? CompanyId => null;
        public string? UserName => null;
        public string? Email => null;
        public IReadOnlyCollection<string> Roles => [];
        public bool IsInRole(string role) => false;
    }

    private sealed class Store : IUserPreferenceStore
    {
        private readonly Dictionary<Guid, UserPreference> rows = [];
        public Guid LastUser { get; private set; }
        public int Writes { get; private set; }
        public bool Unavailable { get; init; }
        public Task<IReadOnlyList<UserPreference>> ReadAsync(Guid userId,
            UserPreferenceCategory[] categories, CancellationToken cancellationToken)
        {
            if (Unavailable) throw new PreferenceStorageUnavailableException();
            LastUser = userId;
            return Task.FromResult<IReadOnlyList<UserPreference>>(
                rows.TryGetValue(userId, out var row) ? new[] { new UserPreference
                {
                    UserId = row.UserId, Category = row.Category, Revision = row.Revision,
                    SchemaVersion = row.SchemaVersion, UpdatedAtUtc = row.UpdatedAtUtc,
                    Data = JsonDocument.Parse(row.Data.RootElement.GetRawText())
                } } : []);
        }
        public Task<bool> SaveAsync(UserPreference value, long expectedRevision, CancellationToken cancellationToken)
        {
            if (Unavailable) throw new PreferenceStorageUnavailableException();
            LastUser = value.UserId;
            if ((rows.GetValueOrDefault(value.UserId)?.Revision ?? 0) != expectedRevision)
                return Task.FromResult(false);
            rows[value.UserId] = new UserPreference
            {
                UserId = value.UserId, Category = value.Category, Revision = value.Revision,
                SchemaVersion = value.SchemaVersion, UpdatedAtUtc = value.UpdatedAtUtc,
                Data = JsonDocument.Parse(value.Data.RootElement.GetRawText())
            };
            Writes++;
            return Task.FromResult(true);
        }
    }
}
