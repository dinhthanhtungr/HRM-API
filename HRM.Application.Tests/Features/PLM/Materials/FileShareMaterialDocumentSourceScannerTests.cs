using HRM.Infrastructure.Services.FileStorage;
using Microsoft.Extensions.Options;

namespace HRM.Application.Tests.Features.PLM.Materials;

public sealed class FileShareMaterialDocumentSourceScannerTests
{
    [Fact]
    public async Task Scanner_OpensOnlyScannedRelativeFiles()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"hrm-material-document-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var filePath = Path.Combine(root, "TDS_NVL_NH_430.pdf");
            await File.WriteAllBytesAsync(filePath, [1, 2, 3, 4]);
            var scanner = CreateScanner(root);

            var scan = await scanner.ScanAsync();
            var file = Assert.Single(scan.Files);
            var content = await scanner.OpenReadAsync(file.RelativePath);
            await using var stream = content.Stream;

            Assert.True(scan.IsAvailable);
            Assert.Equal("application/pdf", content.ContentType);
            Assert.Equal(4, content.Length);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Scanner_RejectsPathTraversal()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"hrm-material-document-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var scanner = CreateScanner(root);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                scanner.OpenReadAsync("../outside.pdf"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static FileShareMaterialDocumentSourceScanner CreateScanner(string root)
    {
        return new FileShareMaterialDocumentSourceScanner(
            Options.Create(new MaterialDocumentImportOptions
            {
                SourceRoot = root,
                AllowedExtensions = [".pdf"]
            }));
    }
}
