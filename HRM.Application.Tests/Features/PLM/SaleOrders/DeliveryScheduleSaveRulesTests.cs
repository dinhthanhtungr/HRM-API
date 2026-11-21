using System.Text.Json;
using HRM.Application.Features.PLM.SaleOrders.DeliverySchedules;
using HRM.Domain.Entities.DeliverySchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.PLM.SaleOrders;

public sealed class DeliveryScheduleSaveRulesTests
{
    private static readonly Guid Id = Guid.NewGuid();
    private static readonly DateOnly Date = new(2026, 11, 20);
    private static DeliveryScheduleRowDto Row(decimal reserved = 0) => new(Id, 1000, reserved, Date, null, null, null);
    private static DeliveryScheduleInput Input(decimal quantity = 1000) => new(Id, quantity, Date, null, null, null);
    [Fact]
    public void MissingFieldsAreNotInterpretedAsClearingDates()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<DeliveryScheduleInput>("{}"));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SaveDeliverySchedulesRequest>("{}"));
    }
    [Fact]
    public void PromiseOnlyCannotChangeQuantityOrRequestedDate()
    {
        Assert.Null(DeliveryScheduleSaveRules.Validate(1000, [Row()], new("r", [Input() with { PromisedDate = Date }], []), false, true));
        Assert.NotNull(DeliveryScheduleSaveRules.Validate(1000, [Row()], new("r", [Input() with { RequestedDate = Date.AddDays(1) }], []), false, true));
    }
    [Fact]
    public void RequestEditorCannotSetOrClearPromisedDate()
    {
        Assert.NotNull(DeliveryScheduleSaveRules.Validate(1000, [Row()], new("r", [Input() with { PromisedDate = Date }], []), true, false));
        Assert.NotNull(DeliveryScheduleSaveRules.Validate(1000, [Row() with { PromisedDate = Date }], new("r", [Input()], []), true, false));
    }
    [Fact]
    public void ExplicitRemovalCannotDeleteReservedSchedule()
    {
        var replacement = Input() with { Id = null };
        Assert.NotNull(DeliveryScheduleSaveRules.Validate(1000, [Row(100)], new("r", [replacement], [Id]), true, true));
        Assert.Null(DeliveryScheduleSaveRules.Validate(1000, [Row()], new("r", [replacement], [Id]), true, true));
    }
    [Fact]
    public void RejectsMissingForeignAndDuplicateScheduleIds()
    {
        Assert.NotNull(DeliveryScheduleSaveRules.Validate(1000, [Row()], new("r", [Input() with { Id = null }], []), true, true));
        Assert.NotNull(DeliveryScheduleSaveRules.Validate(1000, [Row()], new("r", [Input() with { Id = Guid.NewGuid() }], []), true, true));
        Assert.NotNull(DeliveryScheduleSaveRules.Validate(1000, [Row()], new("r", [Input(500), Input(500)], []), true, true));
    }
    [Fact]
    public void AllocationModelHasPositivePrecisionAndBothRestrictForeignKeys()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var type = db.Model.FindEntityType(typeof(DeliveryOrderScheduleAllocation))!;
        Assert.Equal(18, type.FindProperty(nameof(DeliveryOrderScheduleAllocation.Quantity))!.GetPrecision());
        Assert.Equal(3, type.FindProperty(nameof(DeliveryOrderScheduleAllocation.Quantity))!.GetScale());
        Assert.All(type.GetForeignKeys(), fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
        Assert.Contains(type.GetIndexes(), x => x.IsUnique && x.Properties.Count == 2);
    }
}
