using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Npgsql;
using Xunit;

namespace HRM.Application.Tests.Features.InternalMail.GroupStorage;

/// <summary>Starts a fresh loopback-only PostgreSQL cluster. Never reads API settings or a connection-string env var.</summary>
public sealed class IsolatedGroupStoragePostgres : IAsyncLifetime
{
    public static string? BinaryDirectory => new[]
    {
        Environment.GetEnvironmentVariable("HRM_TEST_POSTGRES_BIN"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PostgreSQL", "17", "bin")
    }.FirstOrDefault(path => path is not null && File.Exists(Path.Combine(path, Executable("initdb"))) &&
        File.Exists(Path.Combine(path, Executable("pg_ctl"))));

    private static string Executable(string name) => OperatingSystem.IsWindows() ? name + ".exe" : name;
    private const string DatabaseName = "group_storage_phase2_test";
    private const string TestUser = "group_storage_test";
    private string _dataDirectory = "";
    private string _binaryDirectory = "";
    private int _port;
    private bool _started;
    public string RepositoryRoot { get; } = FindRepositoryRoot();
    public string ScriptPath(string name) => Path.Combine(RepositoryRoot,
        "HRM.Application", "Features", "InternalMail", "design", "group-storage", name);

    public async Task InitializeAsync()
    {
        if (BinaryDirectory is not { } binaries) return;
        _binaryDirectory = binaries;
        var runDirectory = Path.Combine(RepositoryRoot, "artifacts", "group-storage-phase2-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(runDirectory);
        _dataDirectory = Path.GetFullPath(Path.Combine(runDirectory, "data"));
        using (var listener = new TcpListener(IPAddress.Loopback, 0))
        {
            listener.Start();
            _port = ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        await RunAsync("initdb", "-D", _dataDirectory, "--username=" + TestUser, "--auth=trust", "--encoding=UTF8", "--locale=C");
        await RunAsync("pg_ctl", "-D", _dataDirectory, "-l", Path.Combine(runDirectory, "postgres.log"),
            "-o", $"-h 127.0.0.1 -p {_port}", "-w", "-t", "30", "start");
        _started = true;
        try
        {
            await using var admin = await OpenAsync("postgres");
            await using var command = new NpgsqlCommand("CREATE DATABASE group_storage_phase2_test", admin);
            await command.ExecuteNonQueryAsync();
            await using var test = await OpenAsync();
            await using var extension = new NpgsqlCommand("CREATE EXTENSION citext", test);
            await extension.ExecuteNonQueryAsync();
            await test.ReloadTypesAsync();
        }
        catch { await DisposeAsync(); throw; }
    }

    public async Task<NpgsqlConnection> OpenAsync() => await OpenAsync(DatabaseName);

    private async Task<NpgsqlConnection> OpenAsync(string database)
    {
        if (!_started) throw new InvalidOperationException("Isolated PostgreSQL cluster is not running");
        var connection = new NpgsqlConnection(new NpgsqlConnectionStringBuilder
        {
            Host = "127.0.0.1", Port = _port, Database = database, Username = TestUser,
            Pooling = false, Timeout = 5, CommandTimeout = 30, IncludeErrorDetail = false
        }.ConnectionString);
        await connection.OpenAsync();
        try
        {
            // Mandatory guard BEFORE any schema/data command, including CREATE DATABASE or reset.
            await using var guard = new NpgsqlCommand("SELECT current_setting('data_directory'), current_database(), current_user", connection);
            await using var reader = await guard.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal(_dataDirectory.TrimEnd(Path.DirectorySeparatorChar), Path.GetFullPath(reader.GetString(0)).TrimEnd(Path.DirectorySeparatorChar), ignoreCase: OperatingSystem.IsWindows());
            Assert.Equal(database, reader.GetString(1));
            Assert.Equal(TestUser, reader.GetString(2));
            return connection;
        }
        catch { await connection.DisposeAsync(); throw; }
    }

    public async Task ResetAsync()
    {
        if (!_started) return;
        await ExecuteAsync(await File.ReadAllTextAsync(Path.Combine(RepositoryRoot, "HRM.Application.Tests", "Features", "InternalMail", "GroupStorage", "legacy-fixture.sql")));
    }

    public async Task ExecuteAsync(string sql)
    {
        await using var connection = await OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    public async Task ApplyAsync(bool approved = true)
    {
        await using var connection = await OpenAsync();
        await using (var approval = new NpgsqlCommand("SELECT set_config('hrm.group_storage_review_approved', @value, false)", connection))
        {
            approval.Parameters.AddWithValue("value", approved ? "yes" : "no");
            await approval.ExecuteScalarAsync();
        }
        await using var command = new NpgsqlCommand(await File.ReadAllTextAsync(ScriptPath("02-expand-backfill.review.sql")), connection)
            { CommandTimeout = 120 };
        await command.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        if (!_started) return;
        try { await RunAsync("pg_ctl", "-D", _dataDirectory, "-m", "fast", "-w", "-t", "30", "stop"); }
        finally { _started = false; }
        // Keep synthetic data/logs under artifacts for review; never recursively delete anything.
    }

    private async Task RunAsync(string tool, params string[] arguments)
    {
        var info = new ProcessStartInfo(Path.Combine(_binaryDirectory, Executable(tool)))
        {
            UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Unable to start isolated PostgreSQL tool");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(45));
        // Windows server descendants may retain an inherited pipe after pg_ctl exits.
        // Bound output draining as well; pg_ctl -l already persists the useful server log.
        var drain = Task.WhenAll(stdout, stderr);
        var completed = await Task.WhenAny(drain, Task.Delay(TimeSpan.FromSeconds(3)));
        var output = completed == drain ? await stdout : "(output retained by server descendant; see postgres.log)";
        var error = completed == drain ? await stderr : "";
        if (process.ExitCode != 0) throw new InvalidOperationException($"Isolated {tool} failed ({process.ExitCode}): {output} {error}");
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "HRM.sln"))) return directory.FullName;
        throw new InvalidOperationException("Cannot locate repository test scripts");
    }
}

public sealed class PostgresGroupStorageFactAttribute : FactAttribute
{
    public PostgresGroupStorageFactAttribute()
    {
        if (IsolatedGroupStoragePostgres.BinaryDirectory is null)
            Skip = "Requires local PostgreSQL binaries (HRM_TEST_POSTGRES_BIN); never uses the API database.";
    }
}

public sealed class PostgresGroupStorageTheoryAttribute : TheoryAttribute
{
    public PostgresGroupStorageTheoryAttribute()
    {
        if (IsolatedGroupStoragePostgres.BinaryDirectory is null)
            Skip = "Requires local PostgreSQL binaries (HRM_TEST_POSTGRES_BIN); never uses the API database.";
    }
}
