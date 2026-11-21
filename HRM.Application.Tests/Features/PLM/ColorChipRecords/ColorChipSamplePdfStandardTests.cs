using HRM.Application.Abstractions.Documents;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.PLM.ColorChipRecords.Dtos;
using HRM.Application.Features.PLM.ColorChipRecords.Queries.ExportColorChipRecordPdf;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.SampleRequests;

namespace HRM.Application.Tests.Features.PLM.ColorChipRecords;

public sealed class ColorChipSamplePdfStandardTests
{
    [Theory]
    [InlineData(FormStyle.ChipsTanPhu, "")]
    [InlineData(FormStyle.ChipsTanPhuBacNinh, "STANDARD")]
    [InlineData(FormStyle.Chips2, "STANDARD")]
    public async Task Export_PreservesExistingLabStandardBehavior(FormStyle style, string expected)
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<IPLMReadDbContext>();
        var user = new ProductionOrderCreationTests.TestUser();
        var product = new Product { ProductId = Guid.NewGuid(), CompanyId = user.CompanyId!.Value, IsActive = true };
        store.Seed(nameof(db.ColorChipRecords), new ColorChipRecord
        {
            ColorChipRecordId = Guid.NewGuid(), ProductId = product.ProductId, Product = product,
            CompanyId = user.CompanyId.Value, IsActive = true, FormStyle = style
        });
        var renderer = new CapturingRenderer();

        var result = await new ExportColorChipRecordPdfQueryHandler(db, user, renderer)
            .Handle(new ExportColorChipRecordPdfQuery(product.ProductId), default);

        Assert.True(result.Success, result.Message);
        Assert.Equal(expected, renderer.Model!.StandardText);
        Assert.Equal(style, renderer.Style);
        Assert.Equal(0, store.Saves);
    }

    private sealed class CapturingRenderer : IColorChipRecordPdfRenderer
    {
        public ColorChipRecordPdfModel? Model { get; private set; }
        public FormStyle Style { get; private set; }
        public byte[] Render(ColorChipRecordPdfModel model, FormStyle formStyle)
        {
            Model = model;
            Style = formStyle;
            return [37, 80, 68, 70];
        }
    }
}
