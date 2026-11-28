using System.Text;
using System.Reflection;
using HRM.Application.Abstractions.FileStorage;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.PLM.Materials.TdsExtraction;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Infrastructure.Services.Geminis.Materials;
using Microsoft.Extensions.Options;

namespace HRM.Application.Tests.Features.PLM.Materials;

public sealed class TdsExtractionPipelineTests
{
    [Fact]
    public async Task UploadPipelineDeduplicatesBeforeAiAndNeverWritesDbOrAttachments()
    {
        var company = Guid.NewGuid();
        var user = Guid.NewGuid();
        var db = DispatchProxy.Create<IPLMReadDbContext, ProductionOrderTestStore>();
        var store = (ProductionOrderTestStore)(object)db;
        var visible = new Material { MaterialId = Guid.NewGuid(), CompanyId = company, IsActive = true, ExternalId = "NVL_NH_430" };
        store.Seed(nameof(IPLMReadDbContext.Materials), visible,
            new Material { MaterialId = Guid.NewGuid(), CompanyId = Guid.NewGuid(), IsActive = true, ExternalId = "NVL_NH_430" },
            new Material { MaterialId = Guid.NewGuid(), CompanyId = company, IsActive = false, ExternalId = "NVL_NH_430" });
        var settings = Options.Create(new TdsExtractionOptions { Enabled = true });
        var queue = new TdsJobQueue(settings);
        var uploads = new TdsInputFile[] { new("TDS NVL_NH_430.txt", Encoding.UTF8.GetBytes("Density 1.2")),
            new("copy.txt", Encoding.UTF8.GetBytes("Density 1.2")), new("broken.pdf", [1, 2, 3]) };
        queue.TryEnqueue(company, user, uploads);
        var client = new Client();
        var renderer = new Renderer();
        var processor = new TdsExtractionProcessor(db, new Scanner(),
            new TdsDocumentNormalizer(settings, new TdsLegacyOfficeConverter(settings)), client, renderer, queue, settings);
        var job = await queue.DequeueAsync(default);
        await processor.ProcessAsync(job, default);
        Assert.Equal(1, client.Calls);
        Assert.Equal("extracted", renderer.Results[0].Status);
        Assert.Equal(visible.MaterialId, renderer.Results[0].MaterialId);
        Assert.Equal("duplicate", renderer.Results[1].Status);
        Assert.Equal("TDS NVL_NH_430.txt", renderer.Results[1].DuplicateOf);
        Assert.Equal("failed", renderer.Results[2].Status);
        Assert.Contains("invalid_pdf_signature", renderer.Results[2].Warnings);
        Assert.Equal(0, store.Saves);
        Assert.Empty(store.Added);
        Assert.Equal("completed", queue.Get(job.JobId, company, user)!.Status);
        Assert.NotNull(queue.GetExcel(job.JobId, company, user));
    }

    [Fact]
    public async Task SourcePipelineUsesStableBatchAndReturnsNextOffsetWithoutWritingSourceOrDb()
    {
        var company = Guid.NewGuid();
        var user = Guid.NewGuid();
        var db = DispatchProxy.Create<IPLMReadDbContext, ProductionOrderTestStore>();
        var store = (ProductionOrderTestStore)(object)db;
        store.Seed<Material>(nameof(IPLMReadDbContext.Materials));
        var scanner = new Scanner { Files = [new("z.txt", "z.txt", 12, DateTime.UtcNow),
            new("a.txt", "a.txt", 12, DateTime.UtcNow), new("b.txt", "b.txt", 12, DateTime.UtcNow)] };
        var settings = Options.Create(new TdsExtractionOptions { Enabled = true, SourceCompanyId = company });
        var queue = new TdsJobQueue(settings);
        queue.TryEnqueue(company, user, null, 1, 1);
        var job = await queue.DequeueAsync(default);
        var renderer = new Renderer();
        var processor = new TdsExtractionProcessor(db, scanner, new TdsDocumentNormalizer(settings, new TdsLegacyOfficeConverter(settings)),
            new Client(), renderer, queue, settings);
        await processor.ProcessAsync(job, default);
        Assert.Equal(["b.txt"], scanner.OpenedPaths);
        Assert.Equal("b.txt", Assert.Single(renderer.Results).FileName);
        var status = queue.Get(job.JobId, company, user)!;
        Assert.Equal(3, status.SourceTotalFiles);
        Assert.Equal(1, status.SourceOffset);
        Assert.Equal(2, status.NextSourceOffset);
        Assert.Equal(0, store.Saves);
        Assert.Empty(store.Added);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AllSourceBatchesProduceOneWorkbookWithOriginalRowsAndQcNames(bool shared)
    {
        var company = Guid.NewGuid();
        var user = Guid.NewGuid();
        var settings = Options.Create(new TdsExtractionOptions { Enabled = true, SharedSourceExport = shared,
            MaxFiles = 1, MaxBatchBytes = 20, MaxFileBytes = 20 });
        var db = DispatchProxy.Create<IPLMReadDbContext, ProductionOrderTestStore>();
        ((ProductionOrderTestStore)(object)db).Seed<Material>(nameof(IPLMReadDbContext.Materials));
        if (!shared)
            ((ProductionOrderTestStore)(object)db).Seed(nameof(db.Companies),
                new HRM.Domain.Entities.CompanySchema.Company { CompanyId = company });
        var scanner = new Scanner { Files = new[] { "a.txt", "b.txt", "c.txt", "d.txt" }
            .Select(n => new MaterialDocumentSourceFile(n, n, 12, DateTime.UtcNow)).ToArray(),
            Contents = new() { ["a.txt"] = "TDS", ["b.txt"] = "SDS", ["c.txt"] = "COA", ["d.txt"] = "TDS" } };
        var queue = new TdsJobQueue(settings);
        Assert.True(queue.TryEnqueue(company, user, null, allSourceBatches: true, resolvedSourceCompany: company).Accepted);
        var job = await queue.DequeueAsync(default);
        var processor = new TdsExtractionProcessor(db, scanner,
            new TdsDocumentNormalizer(settings, new TdsLegacyOfficeConverter(settings)),
            new MixedDocumentClient(), new TdsExcelRenderer(), queue, settings);
        await processor.ProcessAsync(job, default);
        var status = queue.Get(job.JobId, company, user)!;
        Assert.Equal(4, status.ProcessedFiles);
        Assert.Null(status.NextSourceOffset);
        using var book = new ClosedXML.Excel.XLWorkbook(new MemoryStream(queue.GetExcel(job.JobId, company, user)!));
        Assert.Equal("excluded", book.Worksheet("Documents").Cell("C4").GetString());
        Assert.Equal("duplicate", book.Worksheet("Documents").Cell("C5").GetString());
        var specs = book.Worksheet("Specifications");
        Assert.Equal(5, specs.LastRowUsed()!.RowNumber());
        Assert.Equal("≤ 01,20", specs.Cell("D2").GetString());
        var names = book.Worksheet("Đối chiếu tên");
        Assert.Equal(4, names.LastRowUsed()!.RowNumber());
        Assert.Equal("Viscosity", names.Cell("A2").GetString());
        Assert.Equal("2", names.Cell("B2").GetString());
        Assert.Equal("viscosity", names.Cell("A3").GetString());
        Assert.Equal("Độ nhớt", names.Cell("A4").GetString());
        Assert.True(names.Cell("E2").IsEmpty());
    }

    private sealed class MixedDocumentClient : ITdsExtractionClient
    {
        public Task<TdsDocumentData> ExtractAsync(TdsNormalizedDocument document, CancellationToken cancellationToken)
            => Task.FromResult(new TdsDocumentData { DocumentType = document.Text, Specifications =
                [new() { Name = "Viscosity", Value = "≤ 01,20" },
                 new() { Name = document.Text == "TDS" ? "viscosity" : "Độ nhớt", Value = "0" }] });
    }

    [Fact]
    public void ConflictingCodesCannotAssignMaterialAndNameOnlyIsSuggestion()
    {
        var first = new TdsMaterialCandidate(Guid.NewGuid(), "NVL_NH_430", "Resin");
        var second = new TdsMaterialCandidate(Guid.NewGuid(), "NVL_NH_431", "Other");
        var result = new TdsFileResult { FileName = "TDS NVL_NH_430.txt", Data = new()
            { DocumentType = "TDS", ProductName = "Resin", MaterialCodes = ["NVL_NH_431"] } };
        TdsExtractionRules.ValidateAndMatch(result, [first, second], new("text/plain", null, "Resin", "utf8_text"));
        Assert.Equal("ambiguous", result.MatchStatus);
        Assert.Null(result.MaterialId);
        Assert.Contains("filename_content_code_conflict", result.Warnings);
        result = new() { FileName = "unknown.txt", Data = new() { DocumentType = "TDS", ProductName = "Resin" } };
        TdsExtractionRules.ValidateAndMatch(result, [first], new("text/plain", null, "Resin", "utf8_text"));
        Assert.Equal("suggested", result.MatchStatus);
        Assert.Null(result.MaterialId);
    }

    [Fact]
    public void ValidationPreservesRangesAndFlagsReversedRangeMissingUnitsAndUnsupportedEvidence()
    {
        var spec = new TdsSpecification { Name = "Density", Value = "1.5–1.2", Evidence = "invented", SourceLocation = "paragraph 1" };
        var result = new TdsFileResult { FileName = "tds.txt", Data = new() { DocumentType = "TDS", Specifications = [spec] } };
        TdsExtractionRules.ValidateAndMatch(result, [], new("text/plain", null, "Density 1.2", "utf8_text"));
        Assert.Equal("1.5–1.2", spec.Value);
        Assert.Contains("numeric_range_reversed", spec.ValidationCodes);
        Assert.Contains("numeric_unit_missing", spec.ValidationCodes);
        Assert.Contains("evidence_not_found_in_normalized_text", spec.ValidationCodes);
        Assert.Contains("ai_output_requires_review", result.Warnings);
    }

    private sealed class Client : ITdsExtractionClient
    {
        public int Calls { get; private set; }
        public Task<TdsDocumentData> ExtractAsync(TdsNormalizedDocument document, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new TdsDocumentData { DocumentType = "TDS", ProductName = "Resin", MaterialCodes = ["NVL_NH_430"],
                Specifications = [new() { Name = "Density", Value = "1.2", Unit = "g/cm³", SourceLocation = "text", Evidence = "Density 1.2" }] });
        }
    }

    private sealed class Renderer : ITdsExcelRenderer
    {
        public IReadOnlyList<TdsFileResult> Results { get; private set; } = [];
        public byte[] Render(IReadOnlyList<TdsFileResult> results) { Results = results; return [1, 2, 3]; }
    }

    private sealed class Scanner : IMaterialDocumentSourceScanner
    {
        public IReadOnlyList<MaterialDocumentSourceFile>? Files { get; init; }
        public List<string> OpenedPaths { get; } = [];
        public Dictionary<string, string> Contents { get; init; } = [];
        public Task<MaterialDocumentSourceScan> ScanAsync(CancellationToken cancellationToken = default)
            => Files is null ? throw new InvalidOperationException("Upload must not scan source.") :
                Task.FromResult(new MaterialDocumentSourceScan("Test", true, true, false, null, Files));
        public Task<MaterialDocumentSourceContent> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default)
        {
            if (Files is null) throw new InvalidOperationException("Upload must not open source.");
            OpenedPaths.Add(relativePath);
            var bytes = Encoding.UTF8.GetBytes(Contents.GetValueOrDefault(relativePath, "Density 1.2"));
            return Task.FromResult(new MaterialDocumentSourceContent(new MemoryStream(bytes), relativePath, "text/plain", bytes.Length));
        }
    }
}
