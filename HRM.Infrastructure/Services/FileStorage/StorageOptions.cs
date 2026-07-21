namespace HRM.Infrastructure.Services.FileStorage;

public sealed class StorageOptions
{
    public string RootPath { get; set; } = string.Empty;
    public string? PublicBaseUrl { get; set; }
}
