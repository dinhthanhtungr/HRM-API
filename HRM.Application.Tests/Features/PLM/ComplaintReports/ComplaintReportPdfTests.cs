using HRM.Application.Features.PLM.ComplaintReports.Dtos;
using HRM.Application.Features.PLM.ComplaintReports.Services;
using HRM.Domain.Enums.Orders;
using HRM.Infrastructure.Documents.Pdfs;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using QuestPDF.Infrastructure;

namespace HRM.Application.Tests.Features.PLM.ComplaintReports;

public sealed class ComplaintReportPdfTests
{
    public ComplaintReportPdfTests()
    {
        QuestPDF.Settings.License = LicenseType.Evaluation;
    }

    [Theory]
    [InlineData(ComplaintReportStatus.Draft, true)]
    [InlineData(ComplaintReportStatus.PendingFinalApproval, true)]
    [InlineData(ComplaintReportStatus.Closed, false)]
    public void WatermarkRule_IsDraftUntilClosed(ComplaintReportStatus status, bool expected)
        => Assert.Equal(expected, ComplaintReportPdfRules.ShouldShowDraftWatermark(status));

    [Fact]
    public void FileName_UsesCapaPrefixAndSanitizesInvalidCharacters()
        => Assert.Equal("CAPA-CR-2026-001.pdf", ComplaintReportPdfRules.BuildFileName("CR/2026/001"));

    [Fact]
    public void Render_ReturnsPdfBytes_WithLargeCollectionsAndInvalidImage()
    {
        var document = CreateDocument(
            ComplaintReportStatus.Submitted,
            itemCount: 24,
            includeInvalidImage: true);

        var bytes = CreateRenderer().Render(document);

        Assert.True(bytes.Length > 1_000);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
    }

    [Fact]
    public void Render_ClosedReport_ReturnsValidPdf()
    {
        var bytes = CreateRenderer().Render(CreateDocument(ComplaintReportStatus.Closed, 2));
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.False(ComplaintReportPdfRules.ShouldShowDraftWatermark(ComplaintReportStatus.Closed));
    }

    private static ComplaintReportPdfRenderer CreateRenderer()
        => new(
            Options.Create(new PdfOptions()),
            new FakeHostEnvironment());

    private static ComplaintReportPdfDocumentDto CreateDocument(
        ComplaintReportStatus status,
        int itemCount,
        bool includeInvalidImage = false)
    {
        var lines = Enumerable.Range(1, itemCount)
            .Select(index => new ComplaintReportPdfLineDto
            {
                SourceOrderExternalId = $"DHG2608{index:0000}",
                ProductExternalId = $"SP{index:000}",
                ProductName = $"Test product {index}",
                FormulaExternalId = $"VU{index:000}",
                ManufacturingFormulaExternalId = $"VA{index:000}",
                ComplaintQuantity = 10 + index,
                ApprovedReplacementQuantity = 5 + index,
                IssueType = "Color mismatch",
                Severity = "High",
                Description = "Delivered material did not match the approved sample.",
                Lots =
                [
                    new ComplaintReportPdfLotDto
                    {
                        LotNo = $"LOT-{index:000}",
                        DeliveredQuantity = 50,
                        ComplaintQuantity = 10 + index,
                        DeliveredAt = new DateTime(2026, 8, 1)
                    }
                ]
            })
            .ToList();
        var actions = Enumerable.Range(1, itemCount)
            .Select(index => new ComplaintReportPdfActionDto
            {
                SortOrder = index,
                Content = $"Corrective action {index}: verify process parameters and retain evidence.",
                PersonInChargeName = $"Employee {index}",
                Deadline = new DateTime(2026, 8, 31),
                Result = index % 2 == 0 ? "Completed with evidence" : null,
                CompletedAt = index % 2 == 0 ? new DateTime(2026, 8, 20) : null
            })
            .ToList();

        return new ComplaintReportPdfDocumentDto
        {
            ExternalId = "CR26080001",
            Status = status,
            IsDraft = ComplaintReportPdfRules.ShouldShowDraftWatermark(status),
            CompanyName = "VietAus Polymer",
            CustomerExternalId = "KH_1237",
            CustomerName = "Test Customer",
            IssuePartName = "QA&QC",
            ReportedAt = new DateTime(2026, 8, 3),
            ProposedCompletionAt = new DateTime(2026, 8, 31),
            ReporterName = "Test Reporter",
            RelatedStandards = "Quality",
            RelatedScopes = "CustomerClaim",
            Summary = "Customer complaint about delivered material",
            DocumentRequirement = "Delivery note and retained sample",
            NonConformityDescription = "The delivered lot did not conform to the approved color sample.",
            RootCause = "Process parameter drift during production.",
            InterestedPartyComment = "Customer requests replacement production.",
            CausingParty = "Production",
            RiskReviewedAt = new DateTime(2026, 8, 10),
            HasNewRisk = false,
            RiskReviewComment = "No new risk after applying controls.",
            EffectivenessPersonInChargeName = "QC Reviewer",
            EffectivenessReviewUntil = new DateTime(2026, 9, 30),
            HasRecurrence = false,
            EffectivenessConclusion = "Effective",
            EffectivenessComment = "No recurrence during review period.",
            Lines = lines,
            ImmediateActions = actions.Take(Math.Min(4, itemCount)).ToList(),
            CorrectivePreventiveActions = actions,
            Approvals =
            [
                new ComplaintReportPdfApprovalDto
                {
                    Stage = ComplaintApprovalStage.InitialHodApproval,
                    Decision = ComplaintApprovalDecision.Approved,
                    ActorName = "Initial Approver",
                    DecidedAt = new DateTime(2026, 8, 5),
                    Comment = "Proceed with investigation."
                }
            ],
            Attachments =
            [
                new ComplaintReportPdfAttachmentDto
                {
                    FileName = "evidence.pdf",
                    SizeBytes = 12_000,
                    IsImage = false,
                    IsEmbedded = false
                }
            ],
            Images = includeInvalidImage
                ?
                [
                    new ComplaintReportPdfImageDto
                    {
                        FileName = "broken.png",
                        Content = [1, 2, 3, 4]
                    }
                ]
                : []
        };
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "HRM.Application.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
