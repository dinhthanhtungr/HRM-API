using System.Diagnostics;
using HRM.Application.Features.PLM.Materials.TdsExtraction;
using Microsoft.Extensions.Options;

namespace HRM.Infrastructure.Services.Geminis.Materials;

/// <summary>Chuyển bản sao .doc/.xls bằng LibreOffice cấu hình ở server, không sửa file nguồn.</summary>
internal sealed class TdsLegacyOfficeConverter(IOptions<TdsExtractionOptions> options)
{
    public async Task<byte[]> ConvertAsync(TdsInputFile file, CancellationToken cancellationToken)
    {
        var executable = options.Value.LibreOfficePath;
        if (string.IsNullOrWhiteSpace(executable) || !Path.IsPathFullyQualified(executable) || !File.Exists(executable))
            throw new TdsProcessingException("legacy_office_converter_not_configured");
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "hrm-tds"));
        var workspace = Path.Combine(root, Guid.NewGuid().ToString("N"));
        if (IsBackendPath(root) || IsBackendPath(executable)) throw new TdsProcessingException("converter_path_forbidden");
        Directory.CreateDirectory(workspace);
        try
        {
            var profile = Path.Combine(workspace, "profile");
            Directory.CreateDirectory(Path.Combine(profile, "user"));
            // Use a disposable profile with macro execution disabled. No shared office process/profile.
            await File.WriteAllTextAsync(Path.Combine(profile, "user", "registrymodifications.xcu"),
                """
                <?xml version="1.0" encoding="UTF-8"?>
                <oor:items xmlns:oor="http://openoffice.org/2001/registry">
                  <item oor:path="/org.openoffice.Office.Common/Security/Scripting"><prop oor:name="DisableMacrosExecution" oor:op="fuse"><value>true</value></prop></item>
                  <item oor:path="/org.openoffice.Office.Common/Security/Scripting"><prop oor:name="MacroSecurityLevel" oor:op="fuse"><value>3</value></prop></item>
                  <item oor:path="/org.openoffice.Office.Common/Security/Scripting"><prop oor:name="DisableActiveContent" oor:op="fuse"><value>true</value></prop></item>
                  <item oor:path="/org.openoffice.Office.Calc/Content/Update"><prop oor:name="Link" oor:op="fuse"><value>1</value></prop></item>
                </oor:items>
                """, cancellationToken);
            var inputPath = Path.Combine(workspace, "input" + Path.GetExtension(file.FileName).ToLowerInvariant());
            await File.WriteAllBytesAsync(inputPath, file.Content, cancellationToken);
            var start = new ProcessStartInfo(executable)
            {
                UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = workspace, RedirectStandardOutput = true, RedirectStandardError = true
            };
            foreach (var argument in new[] { "-env:UserInstallation=" + new Uri(profile + Path.DirectorySeparatorChar).AbsoluteUri,
                "--headless", "--nologo", "--nodefault", "--norestore", "--convert-to", "pdf", "--outdir", workspace, inputPath })
                start.ArgumentList.Add(argument);
            using var process = Process.Start(start) ?? throw new TdsProcessingException("office_conversion_failed");
            var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(60));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException)
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
                if (cancellationToken.IsCancellationRequested) throw;
                throw new TdsProcessingException("office_conversion_timeout");
            }
            await Task.WhenAll(stdout, stderr);
            var outputPath = Path.Combine(workspace, "input.pdf");
            if (process.ExitCode != 0 || !File.Exists(outputPath)) throw new TdsProcessingException("office_conversion_failed");
            await using var output = File.OpenRead(outputPath);
            return await TdsFileReader.ReadBoundedAsync(output, options.Value.MaxFileBytes, cancellationToken);
        }
        finally
        {
            // Only this generated child directory can be deleted; source paths are never used here.
            var resolved = Path.GetFullPath(workspace);
            if (resolved.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                try { Directory.Delete(resolved, recursive: true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }

    private static bool IsBackendPath(string path)
    {
        var full = Path.GetFullPath(path);
        return full.Equals(@"F:\Backend", StringComparison.OrdinalIgnoreCase) ||
            full.StartsWith(@"F:\Backend\", StringComparison.OrdinalIgnoreCase);
    }
}
