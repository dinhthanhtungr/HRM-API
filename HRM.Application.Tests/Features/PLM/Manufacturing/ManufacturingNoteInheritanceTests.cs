using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.Manufacturing.Commands.ApplyProductionOrderAction;
using HRM.Application.Features.PLM.Manufacturing.Commands.UpdateProductionOrderInformation;
using HRM.Application.Features.PLM.Manufacturing.Services.Formulas;
using HRM.Application.Features.PLM.ProductionOrders.Services;
using HRM.Application.Features.Timeline.Dtos;
using HRM.Application.Features.Timeline.Services;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Enums.Manufacturings;
using Xunit;

namespace HRM.Application.Tests.Features.PLM.Manufacturing;

public sealed class ManufacturingNoteInheritanceTests
{
    [Theory]
    [InlineData(null, null, "source", "source")]
    [InlineData(null, "saved", "source", "saved")]
    [InlineData("", "saved", "source", "")]
    [InlineData("manual", "saved", "source", "manual")]
    public void HeaderInheritance_PreservesSavedEditsAndExplicitClear(string? input, string? saved, string? source, string? expected) =>
        Assert.Equal(expected, ManufacturingNoteRules.Inherit(input, saved, source));

    [Theory]
    [InlineData(null, false, "source")]
    [InlineData("", false, "source")]
    [InlineData("manual", false, "manual")]
    [InlineData(null, true, null)]
    public void VaNote_DoesNotLoseSourceUnlessExplicitlyCleared(string? input, bool clear, string? expected) =>
        Assert.Equal(expected, ManufacturingNoteRules.FormulaNote(input, clear, "source"));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RequestFormula_PersistsNotesAndPendingVa_RejectsForeignSource(bool foreign)
    {
        var f = new ManufacturingFormulaCreationTests.Fixture(ManufacturingFormulaOrigin.Production);
        f.Order.LabNote = null; f.Order.PlpuNote = null; f.Order.QcCheck = null;
        f.Order.TotalQuantity = 100; f.Order.NumOfBatches = 2;
        var prior = f.Db.ProductionSelectVersions.Single().MfgProductionOrder;
        prior.LabNote = "QA old"; prior.PlpuNote = "PLPU old"; prior.QcCheck = "QC old";
        var handler = new UpdateProductionOrderInformationCommandHandler(f.Db, f.User,
            new CurrentUserPermissionService(f.User), new Clock(), new Events(),
            new ProductionOrderReservationService(f.Db), PromisedDeliveryTestFactory.Create(f.Db, f.User).Notifier);
        var result = await handler.Handle(new(f.Order.MfgProductionOrderId, new() {
            StepOfProduct = f.Order.StepOfProduct, ExpectedDate = null, TotalQuantity = 100, NumOfBatches = 2
        }, ManufacturingInformationAction.RequestFormula) { SourceFormulaId = foreign ? Guid.NewGuid() : f.SourceId }, default);
        Assert.Equal(!foreign, result.Success);
        if (foreign) { Assert.Empty(f.Store.Added); Assert.Null(f.Order.LabNote); return; }
        Assert.Equal("QA old", f.Order.LabNote);
        Assert.Equal("PLPU old", f.Order.PlpuNote);
        Assert.Equal("QC old", f.Order.QcCheck);
        Assert.Equal("FormulaRequested", f.Order.Status);
        var pending = Assert.Single(f.Store.Added.OfType<ProductionSelectVersion>());
        Assert.Equal(f.SourceId, pending.ManufacturingFormulaId);
        Assert.Null(pending.ValidFrom);
        Assert.Null(pending.ValidTo);
    }

    [Fact]
    public async Task PendingSource_RepeatedRequestDoesNotDuplicateOrActivate()
    {
        var f = new ManufacturingFormulaCreationTests.Fixture(ManufacturingFormulaOrigin.Production);
        var pending = new ProductionSelectVersion { ProductionSelectVersionId = Guid.NewGuid(),
            CompanyId = f.Order.CompanyId, MfgProductionOrderId = f.Order.MfgProductionOrderId, ManufacturingFormulaId = f.SourceId };
        f.Store.Seed(nameof(f.Db.ProductionSelectVersions), pending);
        var source = new HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders.ProductionOrderFormulaOptionDto(
            f.SourceId, "VA", ManufacturingFormulaOrigin.Production, null, ClockNow, true);
        await ManufacturingRequestedSource.StoreAsync(f.Db, f.Order, source, f.User.EmployeeId!.Value, ClockNow, default);
        await ManufacturingRequestedSource.StoreAsync(f.Db, f.Order, source, f.User.EmployeeId.Value, ClockNow, default);
        Assert.Empty(f.Store.Added.OfType<ProductionSelectVersion>());
        Assert.Null(pending.ValidFrom);
        Assert.Null(pending.ValidTo);
    }

    [Fact]
    public void InheritedQaRequirement_IsNotAppendedTwice()
    {
        Assert.Equal("Run two screens", ManufacturingNoteRules.MergeLab("Run two screens", "Run two screens"));
        Assert.Equal("Manual\n\nLab lưu ý: instruction",
            ManufacturingNoteRules.MergeLab("Manual\n\nLab lưu ý: instruction", "instruction"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CurrentVa_RemainsEligibleAfterProductStandardChanges_OnlyInOwnCompany(bool foreign)
    {
        var f = new ManufacturingFormulaCreationTests.Fixture(ManufacturingFormulaOrigin.Standard);
        var own = new ManufacturingFormula { ManufacturingFormulaId = Guid.NewGuid(), ExternalId = "VA-OWN",
            CompanyId = foreign ? Guid.NewGuid() : f.Order.CompanyId, IsActive = true, Note = "Own VA note" };
        f.Order.LabNote = "Saved QA request";
        f.Store.Seed(nameof(f.Db.ProductionSelectVersions), new ProductionSelectVersion {
            CompanyId = f.Order.CompanyId, MfgProductionOrderId = f.Order.MfgProductionOrderId,
            ManufacturingFormulaId = own.ManufacturingFormulaId, ManufacturingFormula = own, MfgProductionOrder = f.Order });
        var source = await ManufacturingRequestedSource.ResolveAsync(f.Db, f.Order, own.ManufacturingFormulaId, default);
        if (foreign) Assert.Null(source);
        else { Assert.NotNull(source); Assert.Equal("Own VA note", source.Note); Assert.Equal("Saved QA request", source.LabNote); }
    }
    private static DateTime ClockNow => ManufacturingFormulaCreationTests.Fixture.Now;
    private sealed class Clock : HRM.Application.Abstractions.Commons.Time.IDateTimeProvider {
        public DateTime Now => ManufacturingFormulaCreationTests.Fixture.Now;
    }
    private sealed class Events : IEventLogWriter {
        public Task AddAsync(EventLogCreateRequest request, CancellationToken ct = default) => Task.CompletedTask;
        public Task AddRangeAsync(IEnumerable<EventLogCreateRequest> requests, CancellationToken ct = default) => Task.CompletedTask;
    }
}