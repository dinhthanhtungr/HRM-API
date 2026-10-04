using ClosedXML.Excel;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.Dispatch.DeliveryOrders.Dtos;
using HRM.Application.Features.Dispatch.DeliveryOrders.Queries.ExportDeliveryOrder;
using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.DeliverySchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using HRM.Infrastructure.Documents.Excels;
using HRM.Infrastructure.Documents.Pdfs;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;

namespace HRM.Application.Tests.Features.Dispatch.DeliveryOrders;

public sealed class DeliveryOrderDocumentTests
{
    [Theory]
    [InlineData(false, "DispatchUser", true)]
    [InlineData(true, "Guest", true)]
    [InlineData(true, "DispatchUser", false)]
    public async Task Export_DeniesBeforeReadingOrRendering(bool authenticated, string role, bool hasCompany)
    {
        var user = new ExportUser(authenticated, role, hasCompany ? Guid.NewGuid() : null);
        var handler = new ExportDeliveryOrderQueryHandler(null!, user, null!, null!);
        Assert.Null(await handler.Handle(new ExportDeliveryOrderQuery(Guid.NewGuid()), CancellationToken.None));
    }

    private sealed record ExportUser(bool IsAuthenticated, string Role, Guid? CompanyId) : ICurrentUser
    {
        public Guid UserId => Guid.Empty;
        public Guid? EmployeeId => null;
        public string? UserName => null;
        public string? Email => null;
        public IReadOnlyCollection<string> Roles => [Role];
        public bool IsInRole(string role) => role == Role;
    }

    [Fact]
    public void Projection_EnforcesCompanyActiveAndId()
    {
        var order = Order();
        var query = new[] { order }.AsQueryable();
        Assert.Single(DeliveryOrderDocumentQuery.Project(query, order.Id, order.CompanyId));
        Assert.Empty(DeliveryOrderDocumentQuery.Project(query, order.Id, Guid.NewGuid()));
        Assert.Empty(DeliveryOrderDocumentQuery.Project(query, Guid.NewGuid(), order.CompanyId));
        Assert.Empty(DeliveryOrderDocumentQuery.Project(query, order.Id, Guid.Empty));
        order.IsActive = false;
        Assert.Empty(DeliveryOrderDocumentQuery.Project(query, order.Id, order.CompanyId));
    }

    [Fact]
    public void Projection_UsesActiveLotsAndLegacyFallbackWithoutDuplicatingQuantity()
    {
        var order = Order();
        order.Details =
        [
            new() { ProductExternalIdSnapShot = "A", Quantity = 12m, NumOfBags = 2,
                LotNoList = "OLD", LotConsumptions = [new() { LotNo = "B", IsActive = true },
                    new() { LotNo = "A", IsActive = true }, new() { LotNo = "HIDDEN", IsActive = false }] },
            new() { ProductExternalIdSnapShot = "B", Quantity = 3m, LotNoList = "LEGACY" },
            new() { ProductExternalIdSnapShot = "C", Quantity = 50m, IsAttach = true },
            new() { ProductExternalIdSnapShot = "D", Quantity = 100m, IsActive = false }
        ];
        var model = DeliveryOrderDocumentQuery.Project(new[] { order }.AsQueryable(), order.Id, order.CompanyId).Single();
        Assert.Equal(3, model.Lines.Count);
        Assert.Equal("A, B", model.Lines[0].LotNo);
        Assert.Equal("LEGACY", model.Lines[1].LotNo);
        Assert.Equal(15m, model.TotalQuantity);
        Assert.Equal(2, model.TotalBags);
    }

    [Fact]
    public void Projection_TranslatesWithoutSelectingCost()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var sql = DeliveryOrderDocumentQuery.Project(db.DeliveryOrders.AsNoTracking(), Guid.NewGuid(), Guid.NewGuid()).ToQueryString();
        Assert.Contains("CompanyId", sql);
        Assert.DoesNotContain("UnitCostSnapshot", sql);
        Assert.DoesNotContain("TotalCostSnapshot", sql);
        Assert.DoesNotContain("DeliveryPrice", sql);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(100)]
    public void Pdf_RendersEmptyAndMultiplePages(int count)
    {
        QuestPDF.Settings.License = LicenseType.Evaluation;
        using var font = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "OpenSans-Regular.ttf"));
        QuestPDF.Drawing.FontManager.RegisterFont(font);
        var bytes = new DeliveryOrderPdfRenderer().Render(Model(count));
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
    }

    [Fact]
    public void Excel_PreservesNumbersDatesAndLiteralFormulaLikeText()
    {
        var model = Model(2);
        var bytes = new DeliveryOrderExcelRenderer().Render(model);
        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var sheet = workbook.Worksheet(1);
        Assert.Equal(XLDataType.Number, sheet.Cell(13, 5).DataType);
        Assert.Equal(12.345m, sheet.Cell(13, 5).GetValue<decimal>());
        Assert.Equal(XLDataType.DateTime, sheet.Cell(4, 6).DataType);
        Assert.Equal("=1+1", sheet.Cell(13, 2).GetString());
        Assert.False(sheet.Cell(13, 2).HasFormula);
        Assert.Equal(model.TotalQuantity, sheet.Cell(15, 5).GetValue<decimal>());
        Assert.True(sheet.AutoFilter.IsEnabled);
    }

    private static DeliveryOrder Order() => new()
    {
        Id = Guid.NewGuid(), CompanyId = Guid.NewGuid(),
        Company = new Company { Name = "Công ty thử nghiệm" },
        Customer = new Customer { CustomerName = "Khách hàng thử nghiệm" }
    };

    internal static DeliveryOrderDocumentDto Model(int count) => new()
    {
        Id = Guid.NewGuid(), ExternalId = "PGH-TEST-001", CompanyName = "Công ty thử nghiệm",
        CompanyAddress = "Địa chỉ đơn vị thử nghiệm", CustomerName = "Khách hàng thử nghiệm",
        CreatedDate = new DateTime(2026, 9, 23), Status = "Pending", Receiver = "Nguyễn Văn An",
        DeliveryAddress = "Khu công nghiệp, phường Long Bình, tỉnh Đồng Nai",
        Phone = "0000000000", TaxNumber = "TEST", PaymentType = "Chuyển khoản",
        Note = "Dữ liệu kiểm thử, không phải phiếu giao hàng thực tế.", Deliverers = ["Người giao thử nghiệm"],
        Lines = Enumerable.Range(1, count).Select(i => new DeliveryOrderDocumentLineDto
        {
            ProductCode = i == 1 ? "=1+1" : $"SP-{i:000}",
            ProductName = "Hạt nhựa màu dùng cho sản xuất - tên sản phẩm tiếng Việt dài",
            LotNo = "LOT-A, LOT-B, LOT-C", PONo = "PO-TEST-2026-001", Quantity = 12.345m, NumOfBags = 1
        }).ToList()
    };
}
