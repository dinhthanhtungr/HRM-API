using System.Data;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Npgsql;

// GROUP-STORAGE-TRANSITION: read-only Excel audit; remove with the migration-audit tool after acceptance.
// No schema, backfill, mutation handler or mark-read endpoint is executed.
try
{
    var apiDirectory = Path.GetFullPath("HRM.Api");
    using var launch = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(apiDirectory,"Properties","launchSettings.json")));
    var profile = launch.RootElement.GetProperty("profiles").EnumerateObject().First(p =>
        p.Value.GetProperty("commandName").GetString() == "Project").Value;
    var launchVariables = profile.TryGetProperty("environmentVariables",out var variables)
        ? variables.EnumerateObject().ToDictionary(p => p.Name,p => (string?)p.Value.GetString()) : new Dictionary<string,string?>();
    var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
        ?? launchVariables.GetValueOrDefault("ASPNETCORE_ENVIRONMENT") ?? "Production";
    var configurationBuilder = new ConfigurationBuilder().SetBasePath(apiDirectory)
        .AddJsonFile("appsettings.json").AddJsonFile($"appsettings.{environment}.json", optional: true);
    var secretId = System.Xml.Linq.XDocument.Load(Path.Combine(apiDirectory,"HRM.Api.csproj")).Descendants("UserSecretsId").FirstOrDefault()?.Value;
    if (environment == "Development" && secretId is not null)
        configurationBuilder.AddJsonFile(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Microsoft","UserSecrets",secretId,"secrets.json"),optional:true);
    var configuration = configurationBuilder.AddInMemoryCollection(launchVariables.Select(p =>
        new KeyValuePair<string,string?>(p.Key.Replace("__",":"),p.Value))).AddEnvironmentVariables().Build();
    var options = new NpgsqlConnectionStringBuilder(configuration.GetConnectionString("AppDbConnectionString"))
        { ApplicationName = "GroupStorageReadOnlyAudit", Timeout = 5, CommandTimeout = 15, IncludeErrorDetail = false };
    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(150));
    await using var connection = new NpgsqlConnection(options.ConnectionString);
    await connection.OpenAsync(deadline.Token);
    await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, deadline.Token);
    await using (var guard = new NpgsqlCommand("SET TRANSACTION READ ONLY; SET LOCAL statement_timeout = '15s'; SET LOCAL lock_timeout = '1s';", connection, transaction))
        await guard.ExecuteNonQueryAsync(deadline.Token);
    if (args.Length > 1) throw new ArgumentException("Expected optional output directory");
    var outputRoot = Path.GetFullPath("outputs") + Path.DirectorySeparatorChar;
    var outputDirectory = Path.GetFullPath(args.SingleOrDefault() ??
        "outputs/group-migration-audit-" + DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss"));
    if (!outputDirectory.StartsWith(outputRoot, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Output must stay inside workspace outputs");
    if (File.Exists(Path.Combine(outputDirectory, "source.json"))) throw new IOException("Snapshot already exists; choose a new output directory");
    bool mappingTableExists;
    await using (var check = new NpgsqlCommand("SELECT to_regclass('\"InternalMail\".\"InternalConversationGroups\"') IS NOT NULL", connection, transaction))
        mappingTableExists = (bool)(await check.ExecuteScalarAsync(deadline.Token))!;
    // Both fragments are constant reviewed SQL. No identifier, value or predicate comes from user input.
    const string populatedMapping = "SELECT \"ConversationId\",\"CompanyId\",\"RootConversationId\",\"GroupType\",\"DisplayName\",\"IsActive\" FROM \"InternalMail\".\"InternalConversationGroups\"";
    const string emptyMapping = "SELECT NULL::uuid AS \"ConversationId\", NULL::uuid AS \"CompanyId\", NULL::uuid AS \"RootConversationId\", NULL::smallint AS \"GroupType\", NULL::text AS \"DisplayName\", NULL::boolean AS \"IsActive\" WHERE false";
    var sql = (await File.ReadAllTextAsync("tools/internal-mail-group-storage/export.sql", deadline.Token))
        .Replace("__MAPPING_SOURCE__", mappingTableExists ? populatedMapping : emptyMapping);
    var outputs = new List<JsonElement>();
    await using (var command = new NpgsqlCommand(sql, connection, transaction))
    await using (var reader = await command.ExecuteReaderAsync(deadline.Token))
    {
        do
        {
            while (await reader.ReadAsync(deadline.Token))
            {
                using var document = JsonDocument.Parse(reader.GetString(0));
                outputs.Add(document.RootElement.Clone());
            }
        } while (await reader.NextResultAsync(deadline.Token));
    }
    if (outputs.Count >= 20000) throw new InvalidOperationException("Export limit reached; no truncated report will be published");
    await transaction.RollbackAsync(CancellationToken.None);
    var report = JsonSerializer.Serialize(new { auditedAtUtc = DateTimeOffset.UtcNow, environment, configurationSource = "API settings + environment file + Development user secrets + local launch profile + process environment; running process command-line overrides not inspected", readOnly = true,
        isolation = "repeatable read", outcome = "rolled back", mappingTableExists,
        configuredGroupStorageEnabled = configuration.GetValue<bool>("InternalMail:GroupStorage:Enabled"),
        scope = "All persisted sample-request conversations and groups, including inactive and deleted-message counts. Current mapping presence, not a historical migration timestamp.", metrics = outputs }, new JsonSerializerOptions { WriteIndented = true });
    Directory.CreateDirectory(outputDirectory);
    await File.WriteAllTextAsync(Path.Combine(outputDirectory, "source.json"), report);
    Console.WriteLine($"Read-only audit exported {outputs.Count} group rows; mappingTableExists={mappingTableExists}; transaction rolled back. No message bodies exported.");
}
catch (PostgresException exception) { Console.Error.WriteLine($"Read-only audit failed; SQLSTATE={exception.SqlState}; no exception payload printed."); Environment.ExitCode = 1; }
catch (Exception exception) { Console.Error.WriteLine($"Read-only audit failed ({exception.GetType().Name}); no connection or payload printed."); Environment.ExitCode = 1; }
