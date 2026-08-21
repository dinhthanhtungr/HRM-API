using System.Globalization;
using HRM.Application.Abstractions.Documents;
using HRM.Application.Features.DevAndQA.ProductInspections.Dtos;
using HRM.Infrastructure.Documents.Pdfs.Components;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HRM.Infrastructure.Documents.Pdfs;

internal sealed class ProductInspectionPdfRenderer : IProductInspectionPdfRenderer
{
    private const string DocumentCode = "VA-QA&QC-F18(03)";
    private const string RevisionDate = "22-08-2023";
    private const string LongGiangLogoPath = "Assets/Pdf/LongGiang.png";

    private readonly QuotationPdfBrandingOptions _vietAusBranding;
    private readonly string _contentRootPath;

    public ProductInspectionPdfRenderer(
        IOptions<PdfOptions> options,
        IHostEnvironment hostEnvironment)
    {
        _vietAusBranding = options.Value.Quotation;
        _contentRootPath = hostEnvironment.ContentRootPath;
    }

    public byte[] Render(ProductInspectionPdfDocumentDto document, bool templateOnly)
    {
        ArgumentNullException.ThrowIfNull(document);
        var isLongGiang = IsLongGiang(document);
        var rows = BuildRows(document, isLongGiang, templateOnly);
        var vietAusLogo = LoadImage(_vietAusBranding.LogoPath);
        var longGiangLogo = LoadImage(LongGiangLogoPath);
        var bureauVeritasLogo = LoadImage(_vietAusBranding.BureauVeritasLogoPath);
        var grsLogo = LoadImage(_vietAusBranding.GrsLogoPath);
        var qrCode = LoadImage(_vietAusBranding.QrCodePath);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(20);
                page.DefaultTextStyle(text => text.FontFamily("Open Sans").FontSize(9));

                if (isLongGiang)
                {
                    page.Header().Component(new LongGiangPdfHeaderComponent(longGiangLogo));
                }
                else
                {
                    page.Header().Component(new IsoPdfHeaderComponent(
                        null,
                        null,
                        _vietAusBranding,
                        vietAusLogo));
                }

                page.Content().Element(content => ComposeContent(content, document, rows, isLongGiang, templateOnly));

                if (isLongGiang)
                {
                    page.Footer().Component(new LongGiangPdfFooterComponent(DocumentCode, RevisionDate));
                }
                else
                {
                    page.Footer().Component(new IsoPdfFooterComponent(
                        _vietAusBranding,
                        bureauVeritasLogo,
                        grsLogo,
                        qrCode,
                        DocumentCode,
                        RevisionDate));
                }
            });
        }).GeneratePdf();
    }

    private static void ComposeContent(
        IContainer container,
        ProductInspectionPdfDocumentDto document,
        IReadOnlyList<TestRow> rows,
        bool isLongGiang,
        bool templateOnly)
    {
        container.PaddingTop(4).Column(column =>
        {
            column.Item().AlignCenter().Text("CERTIFICATE OF ANALYSIS").FontSize(14).Bold();
            column.Item().PaddingTop(4).AlignRight().Row(row =>
            {
                row.Spacing(25);
                row.AutoItem().Text($"No: {ValueOrDash(document.ExternalId)}");
                row.AutoItem().Text($"Date of issue: {(document.CreateDate ?? DateTime.Now):dd/MM/yyyy}");
            });

            column.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(180);
                    columns.RelativeColumn();
                });
                AddInfoRow(table, "Product name (Tên sản phẩm):", document.ProductName);
                AddInfoRow(table, "Expiry Date (Hạn sử dụng):", FormatExpiry(document));
                AddInfoRow(table, "Product Code (Mã sản phẩm):", document.ProductCode);
                AddInfoRow(table, "Lot No. (Lô sản xuất):", document.BatchId);
                AddInfoRow(table, "Quantity (Số lượng):", document.Weight.HasValue ? $"{document.Weight.Value:N0} KG" : "-");
                AddInfoRow(table, "Company (Tên công ty):", isLongGiang
                    ? "LONG GIANG CHEMICAL CO., LTD"
                    : "CÔNG TY TNHH CƠ KHÍ NHỰA VIỆT ÚC");
            });

            column.Item().PaddingTop(5).AlignCenter().Text(isLongGiang
                ? "It is hereby certified that the product described below is manufactured by Long Giang Chemical."
                : "We hereby certify that the product described below is manufactured by our company.");

            column.Item().PaddingTop(8).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(22);
                    columns.RelativeColumn(2.1f);
                    columns.RelativeColumn(1.9f);
                    columns.RelativeColumn(1.25f);
                    columns.ConstantColumn(44);
                    columns.RelativeColumn(1.4f);
                });
                table.Header(header =>
                {
                    AddHeader(header, "No");
                    AddHeader(header, "Test Item");
                    AddHeader(header, "Specifications");
                    AddHeader(header, "Test method");
                    AddHeader(header, "Unit");
                    AddHeader(header, "Result");
                });

                for (var index = 0; index < rows.Count; index++)
                {
                    var row = rows[index];
                    AddBody(table, (index + 1).ToString(CultureInfo.InvariantCulture), CellAlignment.Center);
                    AddBody(table, row.Item, CellAlignment.Left);
                    AddBody(table, row.Specification, CellAlignment.Center);
                    AddBody(table, row.Method, CellAlignment.Center);
                    AddBody(table, row.Unit, CellAlignment.Center);
                    AddBody(table, templateOnly ? string.Empty : ValueOrDash(row.Result), CellAlignment.Right);
                }
            });

            column.Item().PaddingTop(8).Text(
                "We certify that the quantity and quality of the above-mentioned product are correct and meet the stated specifications.");
        });
    }

    private static void AddInfoRow(TableDescriptor table, string label, string? value)
    {
        table.Cell().Element(InfoCell).Text(label).SemiBold();
        table.Cell().Element(InfoCell).Text(ValueOrDash(value));
    }

    private static IContainer InfoCell(IContainer container)
        => container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3);

    private static void AddHeader(TableCellDescriptor header, string value)
        => header.Cell().Element(HeaderCell).AlignCenter().AlignMiddle().Text(value).Bold();

    private static IContainer HeaderCell(IContainer container)
        => container.Border(1).BorderColor(Colors.Black).Background(Colors.Grey.Lighten2).Padding(3);

    private static void AddBody(TableDescriptor table, string? value, CellAlignment alignment)
    {
        var cell = table.Cell().Element(BodyCell).AlignMiddle();
        if (alignment == CellAlignment.Center) cell = cell.AlignCenter();
        if (alignment == CellAlignment.Right) cell = cell.AlignRight();
        cell.Text(value ?? string.Empty).FontSize(8);
    }

    private static IContainer BodyCell(IContainer container)
        => container.Border(1).BorderColor(Colors.Black).Padding(2);

    private static List<TestRow> BuildRows(ProductInspectionPdfDocumentDto result, bool isLongGiang, bool templateOnly)
    {
        var specs = result.Specifications;
        var rows = new List<TestRow>();
        void Add(string item, string? value, string defaultSpec, string? overrideSpec, string method, string unit, bool force = false)
        {
            if (!templateOnly && !force && string.IsNullOrWhiteSpace(value)) return;
            rows.Add(new TestRow(
                item,
                string.IsNullOrWhiteSpace(overrideSpec) ? defaultSpec : overrideSpec,
                isLongGiang && method == "Vietaus std" ? string.Empty : method,
                unit,
                templateOnly ? string.Empty : value));
        }

        Add("Appearance/Hình dạng", result.Shape, "Granule/Hạt hình trụ", specs?.Shape, "Vietaus std", "mm");
        Add("Pellet Size/Kích thước hạt", result.ParticleSize, "≤ 3.5", specs?.PelletSize, "Vietaus std", "mm");
        Add("Moisture/Độ ẩm", result.Moisture, "0 - 0.3", specs?.Moisture, "Vietaus std", "%");
        Add("Packing/Quy cách đóng gói", result.PackingSpec, "25", null, "Vietaus std", "Kg");
        Add("Storage temperature/Nhiệt độ bảo quản", result.StorageCondition, "Room temperature", null, "Vietaus std", "°C");
        Add("Storing condition/Điều kiện bảo quản", "Yes", "Put on Pallet", null, "Vietaus std", "-", force: true);
        Add("Shelf-life/Hạn sử dụng", "Yes", "Còn > 1/2 thời gian sử dụng", null, "Vietaus std", "-", force: true);
        Add("Colour Tolerance (Delta E)", result.ColorDeltaE, "< 1.0", specs?.DeltaE, "Vietaus std", "-");
        Add("MI/Chỉ số chảy", result.Mfr, "-", specs?.MeltIndex, "ASTM D1238", "g/10min");
        Add("Dwell Time/Thời gian lưu máy", result.DwellTime == true ? "YES" : null, "-", specs?.DwellTime, "Vietaus std", "-");
        Add("Density/Tỷ trọng", result.Density, "-", specs?.Density, "ASTM D792", "g/cm³");
        Add("Tensile Strength/Độ bền kéo", result.TensileStrength, "-", specs?.TensileStrength, "ASTM D638", "MPa");
        Add("Elongation/Độ giãn dài", result.Elongation, "-", specs?.ElongationAtBreak, "ASTM D638", "%");
        Add("Flexural Strength/Độ bền uốn", result.FlexuralStrength, "-", specs?.FlexuralStrength, "ASTM D790", "MPa");
        Add("Flexural Modulus/Mô đun uốn", result.FlexuralModulus, "-", specs?.FlexuralModulus, "ASTM D790", "MPa");
        Add("Impact Resistance/Chịu va đập", result.ImpactResistance, "-", specs?.IzodImpactStrength, "ASTM D256", "kJ/m²");
        Add("Hardness/Độ cứng", result.Hardness, "-", specs?.Hardness, "Vietaus std", "Shore D");
        Add("IV/Độ nhớt", result.IntrinsicViscosity, "-", null, "Vietaus std", "dL/g");
        Add("Antistatic/Chống tĩnh điện", result.Antistatic, "Có", null, "Vietaus std", "-");
        Add("Black Dots/Chấm đen", result.BlackDots, "-", specs?.BlackDots, "Vietaus std", "-");
        Add("Migration Test/Di hành màu", result.MigrationTest == true ? "YES" : null, "Đạt", specs?.MigrationTest, "Vietaus std", "-");
        return rows;
    }

    private static bool IsLongGiang(ProductInspectionPdfDocumentDto document)
        => (document.BagType ?? string.Empty).Replace('_', ' ').Contains("long giang", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(document.Types, "COA-LG", StringComparison.OrdinalIgnoreCase);

    private static string FormatExpiry(ProductInspectionPdfDocumentDto document)
        => !string.IsNullOrWhiteSpace(document.ExpiryType)
            ? document.ExpiryType
            : document.ExpiryDate?.ToString("dd/MM/yyyy") ?? "-";

    private static string ValueOrDash(string? value)
        => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();

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

    private sealed record TestRow(string Item, string Specification, string Method, string Unit, string? Result);

    private enum CellAlignment
    {
        Left,
        Center,
        Right
    }
}
