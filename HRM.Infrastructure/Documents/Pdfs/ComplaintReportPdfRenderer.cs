using HRM.Application.Abstractions.Documents;
using HRM.Application.Features.PLM.ComplaintReports.Dtos;
using HRM.Application.Features.PLM.ComplaintReports.Services;
using HRM.Domain.Enums.Orders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HRM.Infrastructure.Documents.Pdfs;

internal sealed class ComplaintReportPdfRenderer : IComplaintReportPdfRenderer
{
    private const string BorderColor = "#111827";
    private const string HeaderColor = "#E5E7EB";
    private const string AccentColor = "#1D4ED8";
    private readonly PdfOptions _options;
    private readonly string _contentRootPath;

    public ComplaintReportPdfRenderer(
        IOptions<PdfOptions> options,
        IHostEnvironment hostEnvironment)
    {
        _options = options.Value;
        _contentRootPath = hostEnvironment.ContentRootPath;
    }

    public byte[] Render(ComplaintReportPdfDocumentDto document)
    {
        var logo = LoadLogo();
        var pdf = Document.Create(container =>
        {
            container.Page(page => ConfigurePage(
                page, document, logo, "A. BÁO CÁO (REPORT) - THÔNG TIN CHUNG / GENERAL INFORMATION",
                content => ComposeReportInformation(content, document)));
            container.Page(page => ConfigurePage(
                page, document, logo, "A. BÁO CÁO (REPORT) - BẰNG CHỨNG VÀ PHÊ DUYỆT / EVIDENCE AND APPROVAL",
                content => ComposeEvidence(content, document)));
            container.Page(page => ConfigurePage(
                page, document, logo, "B. HÀNH ĐỘNG KHẮC PHỤC / PHÒNG NGỪA (CORRECTIVE / PREVENTIVE ACTION)",
                content => ComposeCapa(content, document)));
            container.Page(page => ConfigurePage(
                page, document, logo, "C. XÁC MINH HIỆU LỰC (EFFECTIVENESS VERIFICATION)",
                content => ComposeEffectiveness(content, document)));
        });

        return pdf.GeneratePdf();
    }

    private static void ConfigurePage(
        PageDescriptor page,
        ComplaintReportPdfDocumentDto document,
        byte[]? logo,
        string sectionTitle,
        Action<IContainer> content)
    {
        page.Size(PageSizes.A4);
        page.Margin(24);
        page.DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Grey.Darken4));
        if (document.IsDraft)
        {
            page.Background().AlignCenter().AlignMiddle()
                .Text("BẢN NHÁP / DRAFT").FontSize(58).Bold().FontColor("#E5E7EB");
        }

        page.Header().Column(column =>
        {
            column.Item().Height(44).Row(row =>
            {
                row.ConstantItem(66).Height(38).Element(box =>
                {
                    if (logo is { Length: > 0 })
                    {
                        box.Image(logo).FitArea();
                    }
                    else
                    {
                        box.AlignCenter().AlignMiddle().Text("VIETAUS").Bold().FontColor(AccentColor);
                    }
                });
                row.RelativeItem().AlignCenter().Column(title =>
                {
                    title.Item().AlignCenter()
                        .Text("BÁO CÁO SỰ KHÔNG PHÙ HỢP & HÀNH ĐỘNG KHẮC PHỤC PHÒNG NGỪA")
                        .FontSize(11).Bold();
                    title.Item().AlignCenter()
                        .Text("CORRECTIVE AND PREVENTIVE ACTION (CAPA)")
                        .FontSize(9).Bold();
                });
                row.ConstantItem(92).AlignRight().Column(meta =>
                {
                    meta.Item().Text(document.FormCode).Bold();
                    meta.Item().Text(document.ExternalId);
                    meta.Item().Text(document.Status.ToString());
                });
            });
            column.Item().PaddingTop(4).BorderTop(1).BorderColor(BorderColor)
                .Background(HeaderColor).Padding(5).Text(sectionTitle).Bold().FontSize(9);
        });
        page.Content().PaddingTop(8).Element(content);
        page.Footer().BorderTop(0.5f).BorderColor(Colors.Grey.Lighten1).PaddingTop(4)
            .Row(row =>
            {
                row.RelativeItem().Text($"{document.CompanyName} - {document.FormCode}").FontSize(7);
                row.AutoItem().DefaultTextStyle(x => x.FontSize(7)).Text(text =>
                {
                    text.Span("Trang / Page ");
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
            });
    }

    private static void ComposeReportInformation(IContainer container, ComplaintReportPdfDocumentDto document)
    {
        container.Column(column =>
        {
            column.Spacing(6);
            column.Item().Element(x => KeyValueGrid(x,
            [
                ("Bộ phận ban hành / Issue department", Value(document.IssuePartName)),
                ("Ngày phát hành / Issue date", Date(document.ReportedAt)),
                ("Ngày đề xuất hoàn thành / Proposed completion", Date(document.ProposedCompletionAt)),
                ("Người lập / Reporter", Value(document.ReporterName)),
                ("Khách hàng / Customer", $"[{document.CustomerExternalId}] {document.CustomerName}"),
                ("Tiêu chuẩn liên quan / Related standards", JoinFlags(document.RelatedStandards, document.OtherRelatedStandard)),
                ("Phạm vi liên quan / Related scopes", Value(document.RelatedScopes)),
                ("Tóm tắt / Summary", Value(document.Summary))
            ]));
            column.Item().Element(x => SectionLabel(x, "Sản phẩm, số lượng và số lot / Products, quantities and lots"));
            column.Item().Element(x => LinesTable(x, document.Lines));
        });
    }

    private static void ComposeEvidence(IContainer container, ComplaintReportPdfDocumentDto document)
    {
        container.Column(column =>
        {
            column.Spacing(7);
            column.Item().Element(x => SectionLabel(x, "Tài liệu, yêu cầu / Documents, requirements"));
            column.Item().Element(x => TextBox(x, document.DocumentRequirement));
            column.Item().Element(x => AttachmentList(x, document.Attachments));
            column.Item().Element(x => SectionLabel(x, "Mô tả sự không phù hợp / Describe problems or non-conformities"));
            column.Item().Element(x => TextBox(x, document.NonConformityDescription));
            column.Item().Element(x => SectionLabel(x, "Hình ảnh đính kèm / Complaint images"));
            column.Item().Element(x => ImageGallery(x, document.Images));
            column.Item().Element(x => SectionLabel(x, "Người yêu cầu và phê duyệt ban đầu / Requested by and initial HOD approval"));
            column.Item().Element(x => ApprovalBlock(
                x,
                "Người yêu cầu / Requested by",
                document.ReporterName,
                document.ReportedAt,
                null,
                null));
            foreach (var approval in document.Approvals.Where(x => x.Stage == ComplaintApprovalStage.InitialHodApproval))
            {
                column.Item().Element(x => ApprovalBlock(
                    x, "Phê duyệt ban đầu / Initial HOD approval", approval.ActorName, approval.DecidedAt,
                    approval.Decision.ToString(), approval.Comment));
            }
        });
    }

    private static void ComposeCapa(IContainer container, ComplaintReportPdfDocumentDto document)
    {
        container.Column(column =>
        {
            column.Spacing(7);
            column.Item().Element(x => SectionLabel(x, "Hành động tức thời / Immediate actions"));
            column.Item().Element(x => ActionsTable(x, document.ImmediateActions));
            column.Item().Element(x => SectionLabel(x, "Nguyên nhân gốc / Root cause"));
            column.Item().Element(x => TextBox(x, document.RootCause));
            column.Item().Element(x => SectionLabel(x, "Ý kiến bên liên quan / Interested-party comments"));
            column.Item().Element(x => TextBox(x, document.InterestedPartyComment));
            column.Item().Element(x => KeyValueGrid(x,
            [
                ("Bộ phận gây ra / Causing party", Value(document.CausingParty)),
                ("Ngày xem xét rủi ro / Risk reviewed at", Date(document.RiskReviewedAt)),
                ("Có rủi ro mới / New risk", YesNo(document.HasNewRisk)),
                ("Ý kiến đánh giá rủi ro / Risk review comment", Value(document.RiskReviewComment))
            ]));
            column.Item().Element(x => SectionLabel(x, "Hành động khắc phục / phòng ngừa / Corrective / preventive actions"));
            column.Item().Element(x => ActionsTable(x, document.CorrectivePreventiveActions));
        });
    }

    private static void ComposeEffectiveness(IContainer container, ComplaintReportPdfDocumentDto document)
    {
        container.Column(column =>
        {
            column.Spacing(8);
            column.Item().Element(x => KeyValueGrid(x,
            [
                ("Người phụ trách / Responsible person", Value(document.EffectivenessPersonInChargeName)),
                ("Xem xét đến ngày / Review until", Date(document.EffectivenessReviewUntil)),
                ("Có tái diễn / Has recurrence", YesNo(document.HasRecurrence)),
                ("Kết luận / Conclusion", Value(document.EffectivenessConclusion)),
                ("Ý kiến / Comments", Value(document.EffectivenessComment))
            ]));
            column.Item().Element(x => SectionLabel(x, "Kiểm tra / xem xét / Checked / reviewed"));
            var checkedApprovals = document.Approvals
                .Where(x => x.Stage == ComplaintApprovalStage.CheckedAndReviewed).ToList();
            if (checkedApprovals.Count == 0)
            {
                column.Item().Element(x => TextBox(x, "Chưa ghi nhận quyết định kiểm tra / xem xét. / No checked/reviewed decision recorded."));
            }
            foreach (var approval in checkedApprovals)
            {
                column.Item().Element(x => ApprovalBlock(
                    x, "Kiểm tra / xem xét / Checked / reviewed", approval.ActorName, approval.DecidedAt,
                    approval.Decision.ToString(), approval.Comment));
            }

            column.Item().Element(x => SectionLabel(x, "Phê duyệt cuối / Final HOD approval"));
            var finalApprovals = document.Approvals
                .Where(x => x.Stage == ComplaintApprovalStage.FinalHodApproval).ToList();
            if (finalApprovals.Count == 0)
            {
                column.Item().Element(x => TextBox(x, "Chưa ghi nhận phê duyệt cuối. / No final approval recorded."));
            }
            foreach (var approval in finalApprovals)
            {
                column.Item().Element(x => ApprovalBlock(
                    x, "Phê duyệt cuối / Final HOD approval", approval.ActorName, approval.DecidedAt,
                    approval.Decision.ToString(), approval.Comment));
            }

            column.Item().PaddingTop(12).Border(1).BorderColor(BorderColor).Height(95)
                .Padding(8).Text(
                    "Phần này chỉ ghi nhận họ tên, quyết định và ngày thực hiện; không phải chữ ký điện tử hoặc chữ ký tay. " +
                    "/ This section records names, decisions and dates only; it is not an electronic or handwritten signature.")
                .Italic().FontColor(Colors.Grey.Darken1);
        });
    }

    private static void LinesTable(IContainer container, IReadOnlyList<ComplaintReportPdfLineDto> lines)
    {
        if (lines.Count == 0)
        {
            TextBox(container, "Không có dòng khiếu nại active. / No active complaint lines.");
            return;
        }

        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(24);
                columns.RelativeColumn(1.4f);
                columns.RelativeColumn(2.4f);
                columns.RelativeColumn(1.4f);
                columns.RelativeColumn(1.4f);
                columns.RelativeColumn(1.8f);
            });
            table.Header(header =>
            {
                HeaderCell(header.Cell(), "STT / No.");
                HeaderCell(header.Cell(), "Đơn nguồn / Source order");
                HeaderCell(header.Cell(), "Sản phẩm / công thức / Product / formula");
                HeaderCell(header.Cell(), "SL khiếu nại / Complaint qty");
                HeaderCell(header.Cell(), "SL duyệt / Approved qty");
                HeaderCell(header.Cell(), "Lot / vấn đề / Lot / issue");
            });
            var index = 0;
            foreach (var line in lines)
            {
                index++;
                BodyCell(table.Cell(), index.ToString());
                BodyCell(table.Cell(), line.SourceOrderExternalId);
                BodyCell(table.Cell(),
                    $"[{line.ProductExternalId}] {line.ProductName}\nVU: {line.FormulaExternalId}\nCông thức SX / MFG formula: {Value(line.ManufacturingFormulaExternalId)}");
                BodyCell(table.Cell(), Number(line.ComplaintQuantity));
                BodyCell(table.Cell(), line.ApprovedReplacementQuantity.HasValue
                    ? Number(line.ApprovedReplacementQuantity.Value) : "-");
                var lotText = line.Lots.Count == 0
                    ? "Không có lot / No lot"
                    : string.Join("\n", line.Lots.Select(x =>
                        $"{x.LotNo}: {Number(x.ComplaintQuantity)} kg ({Date(x.DeliveredAt)})"));
                BodyCell(table.Cell(),
                    $"{lotText}\nVấn đề / Issue: {Value(line.IssueType)} / {Value(line.Severity)}\n{Value(line.Description)}");
            }
        });
    }

    private static void ActionsTable(IContainer container, IReadOnlyList<ComplaintReportPdfActionDto> actions)
    {
        if (actions.Count == 0)
        {
            TextBox(container, "Chưa ghi nhận hành động. / No action recorded.");
            return;
        }

        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(28);
                columns.RelativeColumn(3);
                columns.RelativeColumn(1.5f);
                columns.RelativeColumn(1.2f);
                columns.RelativeColumn(2);
            });
            table.Header(header =>
            {
                HeaderCell(header.Cell(), "STT / No.");
                HeaderCell(header.Cell(), "Hành động / Action");
                HeaderCell(header.Cell(), "Người phụ trách / Person in charge");
                HeaderCell(header.Cell(), "Thời hạn / Deadline");
                HeaderCell(header.Cell(), "Kết quả / hoàn thành / Result / completed");
            });
            foreach (var action in actions.OrderBy(x => x.SortOrder))
            {
                BodyCell(table.Cell(), action.SortOrder.ToString());
                BodyCell(table.Cell(), action.Content);
                BodyCell(table.Cell(), Value(action.PersonInChargeName));
                BodyCell(table.Cell(), Date(action.Deadline));
                BodyCell(table.Cell(), $"{Value(action.Result)}\n{Date(action.CompletedAt)}");
            }
        });
    }

    private static void AttachmentList(IContainer container, IReadOnlyList<ComplaintReportPdfAttachmentDto> attachments)
    {
        container.Border(1).BorderColor(BorderColor).Padding(6).Column(column =>
        {
            if (attachments.Count == 0)
            {
                column.Item().Text("Không có tệp đính kèm. / No attachment.");
                return;
            }
            foreach (var item in attachments)
            {
                var state = item.IsImage
                    ? item.IsEmbedded ? "ảnh đã nhúng / image embedded" : "chỉ liệt kê ảnh / image listed only"
                    : "chỉ liệt kê tài liệu / document listed only";
                column.Item().Text($"- {item.FileName} ({FormatBytes(item.SizeBytes)}, {state})");
            }
        });
    }

    private static void ImageGallery(IContainer container, IReadOnlyList<ComplaintReportPdfImageDto> images)
    {
        var validImages = images
            .Where(x => ComplaintReportPdfRules.IsSupportedImage(x.Content))
            .ToList();
        if (validImages.Count == 0)
        {
            TextBox(container, "Không có hình ảnh khiếu nại hợp lệ. / No valid complaint image available.");
            return;
        }

        container.Column(column =>
        {
            column.Spacing(6);
            foreach (var pair in validImages.Chunk(2))
            {
                column.Item().Row(row =>
                {
                    foreach (var image in pair)
                    {
                        row.RelativeItem().PaddingRight(4).Border(1).BorderColor(BorderColor)
                            .Padding(4).Column(cell =>
                            {
                                cell.Item().Height(145).Image(image.Content).FitArea();
                                cell.Item().PaddingTop(3).AlignCenter().Text(image.FileName).FontSize(7);
                            });
                    }
                    if (pair.Length == 1)
                    {
                        row.RelativeItem();
                    }
                });
            }
        });
    }

    private static void ApprovalBlock(
        IContainer container,
        string title,
        string name,
        DateTime date,
        string? decision,
        string? comment)
    {
        container.Border(1).BorderColor(BorderColor).Padding(6).Column(column =>
        {
            column.Item().Text(title).Bold().FontColor(AccentColor);
            column.Item().Text($"Họ tên / Name: {Value(name)}");
            column.Item().Text($"Quyết định / Decision: {Value(decision)}");
            column.Item().Text($"Ngày / Date: {Date(date)}");
            column.Item().Text($"Ý kiến / Comment: {Value(comment)}");
        });
    }

    private static void KeyValueGrid(IContainer container, IReadOnlyList<(string Label, string Value)> values)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(1.3f);
                columns.RelativeColumn(2.7f);
                columns.RelativeColumn(1.3f);
                columns.RelativeColumn(2.7f);
            });
            for (var index = 0; index < values.Count; index += 2)
            {
                LabelCell(table.Cell(), values[index].Label);
                BodyCell(table.Cell(), values[index].Value);
                if (index + 1 < values.Count)
                {
                    LabelCell(table.Cell(), values[index + 1].Label);
                    BodyCell(table.Cell(), values[index + 1].Value);
                }
                else
                {
                    LabelCell(table.Cell(), string.Empty);
                    BodyCell(table.Cell(), string.Empty);
                }
            }
        });
    }

    private static void SectionLabel(IContainer container, string text)
        => container.Background(HeaderColor).Border(1).BorderColor(BorderColor)
            .Padding(4).Text(text).Bold();

    private static void TextBox(IContainer container, string? text)
        => container.Border(1).BorderColor(BorderColor).MinHeight(38).Padding(6).Text(Value(text));

    private static void HeaderCell(IContainer container, string text)
        => container.Border(1).BorderColor(BorderColor).Background(HeaderColor)
            .Padding(4).AlignCenter().AlignMiddle().Text(text).Bold().FontSize(7);

    private static void LabelCell(IContainer container, string text)
        => container.Border(1).BorderColor(BorderColor).Background("#F9FAFB")
            .Padding(4).Text(text).Bold().FontSize(7);

    private static void BodyCell(IContainer container, string text)
        => container.Border(1).BorderColor(BorderColor).Padding(4).AlignMiddle().Text(Value(text)).FontSize(7);

    private static string Value(string? value) => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
    private static string Date(DateTime? value) => value?.ToString("dd/MM/yyyy HH:mm") ?? "-";
    private static string Number(decimal value) => value.ToString("#,##0.###");
    private static string YesNo(bool? value) => value.HasValue ? value.Value ? "Có / Yes" : "Không / No" : "-";
    private static string JoinFlags(string flags, string? other)
        => string.IsNullOrWhiteSpace(other) ? Value(flags) : $"{Value(flags)}; Khác / Other: {other.Trim()}";
    private static string FormatBytes(long bytes)
        => bytes >= 1024 * 1024
            ? $"{bytes / 1024d / 1024d:0.##} MB"
            : $"{bytes / 1024d:0.##} KB";

    private byte[]? LoadLogo()
    {
        var configuredPath = _options.Quotation.LogoPath;
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return null;
        }
        var path = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(_contentRootPath, configuredPath);
        var fullPath = Path.GetFullPath(path);
        return File.Exists(fullPath) ? File.ReadAllBytes(fullPath) : null;
    }
}
