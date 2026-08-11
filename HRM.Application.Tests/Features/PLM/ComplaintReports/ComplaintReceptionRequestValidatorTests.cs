using HRM.Application.Features.PLM.ComplaintReports.Dtos;
using HRM.Application.Features.PLM.ComplaintReports.Services;
using HRM.Domain.Enums.Orders;

namespace HRM.Application.Tests.Features.PLM.ComplaintReports;

public sealed class ComplaintReceptionRequestValidatorTests
{
    [Fact]
    public void Validate_AcceptsMatchingLineAndLotQuantities()
    {
        var error = ComplaintReceptionRequestValidator.Validate(
            "Hàng không đạt màu",
            "Màu thực tế lệch mẫu",
            ComplaintResolutionType.ReplacementProduction,
            [CreateLine(10, 4, 6)]);

        Assert.Null(error);
    }

    [Fact]
    public void Validate_RejectsMissingSummary()
    {
        var error = ComplaintReceptionRequestValidator.Validate(
            " ",
            null,
            ComplaintResolutionType.FeedbackOnly,
            [CreateLine(10, 10)]);

        Assert.NotNull(error);
    }

    [Fact]
    public void Validate_RejectsLineWithoutLots()
    {
        var line = new ComplaintReportLineRequest
        {
            SourceMerchandiseOrderDetailId = Guid.NewGuid(),
            ComplaintQuantity = 10
        };

        var error = ComplaintReceptionRequestValidator.Validate(
            "Khiếu nại",
            null,
            ComplaintResolutionType.FeedbackOnly,
            [line]);

        Assert.NotNull(error);
    }

    [Fact]
    public void Validate_RejectsLotTotalDifferentFromLineQuantity()
    {
        var error = ComplaintReceptionRequestValidator.Validate(
            "Khiếu nại",
            null,
            ComplaintResolutionType.FeedbackOnly,
            [CreateLine(10, 4, 5)]);

        Assert.NotNull(error);
    }

    [Fact]
    public void Validate_RejectsDuplicateSourceLine()
    {
        var line = CreateLine(10, 10);

        var error = ComplaintReceptionRequestValidator.Validate(
            "Khiếu nại",
            null,
            ComplaintResolutionType.FeedbackOnly,
            [line, line]);

        Assert.NotNull(error);
    }

    [Fact]
    public void Validate_RejectsDuplicateLotReference()
    {
        var deliveryDetailId = Guid.NewGuid();
        var consumptionId = Guid.NewGuid();
        var line = new ComplaintReportLineRequest
        {
            SourceMerchandiseOrderDetailId = Guid.NewGuid(),
            ComplaintQuantity = 10,
            Lots =
            [
                new ComplaintReportLotRequest
                {
                    SourceDeliveryOrderDetailId = deliveryDetailId,
                    SourceLotConsumptionId = consumptionId,
                    ComplaintQuantity = 4
                },
                new ComplaintReportLotRequest
                {
                    SourceDeliveryOrderDetailId = deliveryDetailId,
                    SourceLotConsumptionId = consumptionId,
                    ComplaintQuantity = 6
                }
            ]
        };

        var error = ComplaintReceptionRequestValidator.Validate(
            "Khiếu nại",
            null,
            ComplaintResolutionType.FeedbackOnly,
            [line]);

        Assert.NotNull(error);
    }

    [Fact]
    public void Validate_RejectsUnknownResolutionType()
    {
        var error = ComplaintReceptionRequestValidator.Validate(
            "Khiếu nại",
            null,
            (ComplaintResolutionType)999,
            [CreateLine(10, 10)]);

        Assert.NotNull(error);
    }

    private static ComplaintReportLineRequest CreateLine(
        decimal lineQuantity,
        params decimal[] lotQuantities)
    {
        return new ComplaintReportLineRequest
        {
            SourceMerchandiseOrderDetailId = Guid.NewGuid(),
            ComplaintQuantity = lineQuantity,
            Lots = lotQuantities.Select(quantity => new ComplaintReportLotRequest
            {
                SourceDeliveryOrderDetailId = Guid.NewGuid(),
                SourceLotConsumptionId = Guid.NewGuid(),
                ComplaintQuantity = quantity
            }).ToArray()
        };
    }
}
