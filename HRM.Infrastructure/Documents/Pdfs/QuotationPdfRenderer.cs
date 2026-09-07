using HRM.Application.Abstractions.Documents;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Infrastructure.Documents.Pdfs.Components;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HRM.Infrastructure.Documents.Pdfs;

internal sealed class QuotationPdfRenderer : IQuotationPdfRenderer
{
    private static readonly string[] TierLabelSuffixesToHide =
    [
        " - liên hệ Ban giám đốc",
        "- liên hệ Ban giám đốc",
        " - liên hệ BGĐ",
        "- liên hệ BGĐ",
        " - lien he Ban giam doc",
        "- lien he Ban giam doc",
        " - lien he BGD",
        "- lien he BGD"
    ];

    private readonly QuotationPdfBrandingOptions _options;
    private readonly string _contentRootPath;

    public QuotationPdfRenderer(
        IOptions<PdfOptions> options,
        IHostEnvironment hostEnvironment)
    {
        _options = options.Value.Quotation;
        _contentRootPath = hostEnvironment.ContentRootPath;
    }

    public byte[] Render(QuotationPdfDocumentDto quotation)
    {
        var logo = LoadImage(_options.LogoPath);
        var bureauVeritasLogo = LoadImage(_options.BureauVeritasLogoPath);
        var grsLogo = LoadImage(_options.GrsLogoPath);
        var qrCode = LoadImage(_options.QrCodePath);
        var useLandscape = quotation.Lines
            .SelectMany(line => line.PriceTiers)
            .Select(tier => tier.QuantityRangeLabel)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count() > 6;

        var document = Document.Create(documentContainer =>
        {
            documentContainer.Page(page =>
            {
                page.Size(PdfLayout.A4(landscape: false));
                page.MarginHorizontal(PdfLayout.PageMarginHorizontal);
                page.MarginVertical(PdfLayout.PageMarginVertical);
                page.DefaultTextStyle(PdfTypography.Body);
                page.Header().Component(new IsoPdfHeaderComponent(
                    quotation.CompanyAddress,
                    quotation.CompanyPhone,
                    _options,
                    logo));
                page.Content().Layers(layers =>
                {
                    if (quotation.Status != QuotationStatus.Sent)
                    {
                        var watermarkText = quotation.Status switch
                        {
                            QuotationStatus.Draft => "BẢN NHÁP / DRAFT",
                            QuotationStatus.PendingApproval => "CHỜ DUYỆT / PENDING APPROVAL",
                            QuotationStatus.Approved => "ĐÃ ĐỦ GIÁ CHUẨN / APPROVED",
                            _ => "BÁO GIÁ / QUOTATION"
                        };

                        layers.Layer()
                            .AlignCenter()
                            .AlignMiddle()
                            .Rotate(-35)
                            .Text(watermarkText)
                            .FontSize(42)
                            .Bold()
                            .FontColor("#E5E7EB");
                    }

                    layers.PrimaryLayer()
                        .PaddingTop(PdfLayout.ContentTopPadding)
                        .Column(column => ComposeContent(column, quotation));
                });
                page.Footer().Component(new IsoPdfFooterComponent(
                    _options,
                    bureauVeritasLogo,
                    grsLogo,
                    qrCode));
            });
        });

        return document.GeneratePdf();
    }

    private static void ComposeContent(
        ColumnDescriptor column,
        QuotationPdfDocumentDto quotation)
    {
        column.Spacing(PdfLayout.QuotationColumnSpacing);
        column.Item().AlignCenter().Text("BẢNG BÁO GIÁ/QUOTATION")
            .FontSize(PdfTypography.QuotationTitleSize).Bold().FontColor(PdfColors.TitleRed);
        //if (quotation.Status == QuotationStatus.Draft)
        //{
        //    column.Item().AlignCenter().Text("Tài liệu nội bộ — chưa xác nhận gửi khách hàng")
        //        .FontSize(PdfTypography.SmallSize).FontColor("#9CA3AF");
        //}
        column.Item().Element(container => ComposeQuotationInformation(container, quotation));
        column.Item().LineHorizontal(PdfLayout.SectionDividerWidth).LineColor(PdfColors.BrandGreen);
        column.Item().Text(
                "Xin cảm ơn Quý khách hàng đã quan tâm và sử dụng sản phẩm của Công ty chúng tôi. " +
                "Theo yêu cầu, chúng tôi xin gửi tới Quý vị báo giá tốt nhất của chúng tôi cho các sản phẩm sau:")
            .FontSize(PdfTypography.SmallSize);
        column.Item().Text(
                "Thanks for customer's support. As requested, we are very pleased to submit to you " +
                "our best price for the following product(s):")
            .Italic().FontSize(PdfTypography.SmallSize);

        var tieredLines = quotation.Lines
            .Where(line => line.PriceTiers.Count > 0)
            .ToList();

        if (tieredLines.Count > 0)
        {
            column.Item().Element(container => ComposeTieredPriceTable(
                container,
                tieredLines,
                quotation.Currency));
        }

        if (quotation.Lines.Count == 0)
        {
            column.Item().Border(PdfLayout.TableBorderWidth).Padding(PdfLayout.EmptyStatePadding).AlignCenter()
                .Text("Báo giá chưa có sản phẩm / No product line.");
        }

        column.Item().Element(container => ComposePricingNotes(container, quotation));
        column.Item().Element(container => ComposeTerms(container, quotation));
        column.Item().PaddingTop(PdfLayout.SignatureTopPadding).Text(
            "Chúng tôi rất mong nhận được sự quan tâm, hồi đáp sớm của Quý vị.\n" +
            "We look to your kind attention, favorable reply.\n" +
            "Trân trọng kính chào / Best regards,");
        column.Item().PaddingTop(PdfLayout.SignatureTopPadding).Text(quotation.SaleEmployeeName)
            .Bold().FontColor(PdfColors.LinkBlue);

        var saleContact = string.Join(" | ", new[]
        {
            quotation.SaleEmployeePhone,
            quotation.SaleEmployeeEmail
        }.Where(value => !string.IsNullOrWhiteSpace(value)));

        if (!string.IsNullOrWhiteSpace(saleContact))
        {
            column.Item().Text(saleContact).FontSize(PdfTypography.SmallSize);
        }
    }

    private static void ComposeQuotationInformation(
        IContainer container,
        QuotationPdfDocumentDto quotation)
    {
        container.Row(row =>
        {
            row.RelativeItem(3).Column(customer =>
            {
                AddLabelValue(customer, "Kính gửi/To:", quotation.ContactName ?? quotation.CustomerName);
                AddLabelValue(customer, "Đơn vị/Co.:", quotation.CustomerName);
                AddLabelValue(customer, "Địa chỉ/Address:", quotation.CustomerAddress);
                AddLabelValue(customer, "Điện thoại/Tel:", quotation.CustomerPhone);
            });

            row.RelativeItem(2).Column(metadata =>
            {
                AddLabelValue(metadata, "Số/No.:", quotation.ExternalId);
                AddLabelValue(metadata, "Ngày/Date:", quotation.QuotationDate.ToString("dd/MM/yyyy"));
                AddLabelValue(metadata, "Từ/From:", quotation.SaleEmployeeName);
                AddLabelValue(metadata, "ĐT/Tel:", quotation.SaleEmployeePhone);
                AddLabelValue(metadata, "Email:", quotation.SaleEmployeeEmail);
            });
        });
    }

    private static void ComposeTieredPriceTable(
        IContainer container,
        IReadOnlyList<QuotationPdfLineDto> lines,
        string currency)
    {
        var tierLabels = lines
            .SelectMany(line => line.PriceTiers)
            .Select(tier => new
            {
                QuantityRangeLabel = FormatTierLabel(tier.QuantityRangeLabel),
                tier.SortOrder
            })
            .Where(tier => !string.IsNullOrWhiteSpace(tier.QuantityRangeLabel))
            .GroupBy(tier => tier.QuantityRangeLabel, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderBy(tier => tier.SortOrder).First())
            .OrderBy(tier => tier.SortOrder)
            .ThenBy(tier => tier.QuantityRangeLabel)
            .Select(tier => tier.QuantityRangeLabel)
            .ToList();
        var units = lines
            .Select(line => line.Unit)
            .Where(unit => !string.IsNullOrWhiteSpace(unit))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var quantityUnit = units.Count == 1 ? $" ({units[0]})" : string.Empty;

        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(58);
                columns.RelativeColumn(2.3f);
                foreach (var _ in tierLabels)
                {
                    columns.RelativeColumn();
                }
                columns.RelativeColumn(0.9f);
            });

            table.Header(header =>
            {
                HeaderCell(header.Cell().RowSpan(2), "Mã số/Code");
                HeaderCell(header.Cell().RowSpan(2), "Tên hàng/Name");
                HeaderCell(header.Cell().ColumnSpan((uint)tierLabels.Count),
                    $"Số lượng/Quantity{quantityUnit}");
                HeaderCell(header.Cell().RowSpan(2), "Ghi chú\nNote");

                foreach (var label in tierLabels)
                {
                    HeaderCell(header.Cell(), label);
                }
            });

            foreach (var line in lines)
            {
                BodyCell(table.Cell(), line.ProductCode);
                BodyCell(table.Cell(), line.ProductName);

                foreach (var tierLabel in tierLabels)
                {
                    var tier = line.PriceTiers.FirstOrDefault(item =>
                        string.Equals(
                            FormatTierLabel(item.QuantityRangeLabel),
                            tierLabel,
                            StringComparison.OrdinalIgnoreCase));
                    BodyCell(
                        table.Cell(),
                        tier is null
                            ? "-"
                            : tier.CustomerUnitPrice <= 0m
                                ? string.Empty
                                : FormatMoney(tier.CustomerUnitPrice, currency));
                }

                BodyCell(table.Cell(), line.Note ?? "-");
            }
        });
    }

    private static void ComposePricingNotes(
        IContainer container,
        QuotationPdfDocumentDto quotation)
    {
        if (!string.IsNullOrWhiteSpace(quotation.Note))
        {
            container.Text(quotation.Note).FontSize(PdfTypography.SmallSize);
        }
    }

    private static void ComposeTerms(
        IContainer container,
        QuotationPdfDocumentDto quotation)
    {
        container.Column(column =>
        {
            column.Item().Text("Các điều khoản khác:")
                .Bold().Underline();

            if (quotation.HasStoredTerms)
            {
                var activeTerms = quotation.Terms
                    .Where(x => x.IsActive)
                    .OrderBy(x => x.SortOrder)
                    .ToArray();
                for (var index = 0; index < activeTerms.Length; index++)
                {
                    var term = activeTerms[index];
                    var label = JoinBilingual(term.LabelVi, term.LabelEn);
                    var value = JoinBilingual(term.ValueVi, term.ValueEn);
                    AddTerm(column, $"{index + 1}. {label}:", value);
                }

                return;
            }

            AddTerm(
                column,
                "1. Thời hạn giao hàng/Delivery date (Ngày nhận đơn hàng/The receive PO date):",
                DefaultIfBlank(quotation.DeliveryTerms, QuotationPdfDefaults.DeliveryTerms));
            AddTerm(column, "2. Địa điểm giao hàng/place to delivery:", QuotationPdfDefaults.DeliveryPlace);
            AddTerm(column, "3. Đóng gói/Packaging:", QuotationPdfDefaults.Packaging);
            AddTerm(column, "4. Số lượng tối thiểu cho đơn hàng/Minimum quantity for order:", QuotationPdfDefaults.MinimumQuantity);
            AddTerm(
                column,
                "5. Thanh toán/payment term:",
                DefaultIfBlank(quotation.PaymentTerms, QuotationPdfDefaults.PaymentTerms));
            AddTerm(
                column,
                "6. Thời hạn hiệu lực của báo giá/Validity:",
                quotation.ValidUntil?.ToString("dd/MM/yyyy") ?? "-");
        });
    }

    private static void AddLabelValue(
        ColumnDescriptor column,
        string label,
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        column.Item().Row(row =>
        {
            row.ConstantItem(78).Text(label).FontSize(PdfTypography.SmallSize).Bold();
            row.RelativeItem().Text(value).FontSize(PdfTypography.SmallSize);
        });
    }

    private static void AddTerm(
        ColumnDescriptor column,
        string label,
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        column.Item().Row(row =>
        {
            row.RelativeItem(2).Text(label).FontSize(PdfTypography.SmallSize);
            row.RelativeItem(3).Text(value).FontSize(PdfTypography.SmallSize).FontColor(PdfColors.LinkBlue);
        });
    }

    private static void HeaderCell(IContainer container, string text)
    {
        container.Border(PdfLayout.TableBorderWidth).Background(PdfColors.TableHeaderYellow)
            .PaddingVertical(PdfLayout.TableCellPaddingVertical)
            .PaddingHorizontal(PdfLayout.TableCellPaddingHorizontal)
            .AlignCenter().AlignMiddle()
            .Text(text).Bold().FontSize(PdfTypography.SmallSize);
    }

    private static void BodyCell(IContainer container, string text)
    {
        container.Border(PdfLayout.TableBorderWidth)
            .PaddingVertical(PdfLayout.TableCellPaddingVertical)
            .PaddingHorizontal(PdfLayout.TableCellPaddingHorizontal)
            .AlignCenter().AlignMiddle().Text(text).FontSize(PdfTypography.SmallSize);
    }

    private static string FormatMoney(decimal value, string currency)
        => string.Equals(currency, "VND", StringComparison.OrdinalIgnoreCase)
            ? value.ToString("#,##0.##")
            : value.ToString("#,##0.00");

    private static string FormatNumber(decimal value)
        => value.ToString("#,##0.##");

    private static string FormatTierLabel(string value)
    {
        var label = value.Trim();
        foreach (var suffix in TierLabelSuffixesToHide)
        {
            if (label.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return label[..^suffix.Length].Trim();
            }
        }

        return label;
    }

    private static string DefaultIfBlank(string? value, string defaultValue)
        => string.IsNullOrWhiteSpace(value) ? defaultValue : value;

    private static string JoinBilingual(string? vietnamese, string? english)
    {
        var values = new[] { vietnamese, english }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase);
        return string.Join("/", values);
    }

    private byte[]? LoadImage(string? configuredPath)
    {
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
