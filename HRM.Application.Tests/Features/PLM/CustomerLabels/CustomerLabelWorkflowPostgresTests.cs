using System.Reflection;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Application.Features.PLM.CustomerLabels.Commands.CreateCustomerLabel;
using HRM.Application.Features.PLM.CustomerLabels.Commands.PatchCustomerLabelDetail;
using HRM.Application.Features.PLM.CustomerLabels.Queries.GetCustomerLabels;
using HRM.Application.Features.PLM.SampleRequests.Commands.ChangeSampleRequestColourCode;
using HRM.Application.Features.PLM.SampleRequests.Commands.CreateSampleRequestDirectPatchNotification;
using HRM.Application.Features.PLM.SampleRequests.Commands.PatchSampleRequest;
using HRM.Application.Features.PLM.SampleRequests.Services;
using HRM.Application.Tests.Features.InternalMail.GroupStorage;
using HRM.Application.Tests.Features.PLM.SampleRequests;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Entities.PrintectSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using HRM.Infrastructure.Services.ExternalIds;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using static HRM.Application.Tests.Features.PLM.SampleRequests.SampleWorkflowMutationTestSupport;

namespace HRM.Application.Tests.Features.PLM.CustomerLabels;

/// <summary>Real command handlers and PostgreSQL; notification transport only is mocked.</summary>
public sealed class CustomerLabelWorkflowPostgresTests(IsolatedGroupStoragePostgres postgres) : IClassFixture<IsolatedGroupStoragePostgres>
{
    [PostgresGroupStorageFact]
    public async Task SaleLabelSurvivesLabCodeCreationChangeAndTransactionRollback()
    {
        await using var connection = await postgres.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        await Schema(connection, db);
        foreach (var name in new[] { "CustomerLabelHeader", "CustomerLabelDetail", "Quotation", "QuotationLine", "Notification" })
            await SampleDeliveryOptionsPostgresTests.CreateTable(connection, db.Model.GetEntityTypes().Single(e => e.ClrType.Name == name));
        await using (var counter = new NpgsqlCommand("""
            CREATE TABLE public."IdCounters" (
                "CompanyId" uuid NOT NULL, "Prefix" text NOT NULL, "Period" text NOT NULL, "LastNo" integer NOT NULL,
                PRIMARY KEY ("CompanyId", "Prefix", "Period"));
            """, connection))
            await counter.ExecuteNonQueryAsync();

        var company = Guid.NewGuid();
        var sale = new Actor(company, Guid.NewGuid(), "SaleUser");
        var lab = new Actor(company, Guid.NewGuid(), "LabUser");
        var date = new DateTime(2026, 11, 17, 10, 0, 0);
        var customer = new Customer { CustomerId = Guid.NewGuid(), CompanyId = company, ExternalId = "KH_375", IsActive = true };
        var category = new Category { CategoryId = Guid.NewGuid(), CompanyId = company, ExternalId = "TP", Name = "Test", IsActive = true };
        var product = new Product { ProductId = Guid.NewGuid(), CompanyId = company, CategoryId = category.CategoryId,
            Name = "Test product", IsActive = true, CreatedDate = date };
        var request = new SampleRequest { SampleRequestId = Guid.NewGuid(), CompanyId = company,
            CustomerId = customer.CustomerId, ProductId = product.ProductId, ManagerBy = sale.EmployeeId!.Value,
            Status = "InProgress", InfoType = "Báo giá", ExternalId = "TP_TEST_LABEL", CreatedDate = date, UpdatedDate = date, IsActive = true };
        db.Employees.AddRange(new Employee { EmployeeId = sale.EmployeeId.Value, CompanyId = company, IsActive = true },
            new Employee { EmployeeId = lab.EmployeeId!.Value, CompanyId = company, IsActive = true });
        db.Categories.Add(category); db.Customers.Add(customer); db.Products.Add(product); db.SampleRequests.Add(request);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var created = await new CreateCustomerLabelCommandHandler(db, sale, new Clock(date), new ExternalIdServicePostgres(db)).Handle(new()
        {
            ProductId = product.ProductId, CustomerId = customer.CustomerId,
            ColorCode = "STALE_CLIENT_CODE", CustomerExternalId = "SPOOFED",
            Details = [
                new() { LineNo = 1, FieldKey = "Name", FieldValue = "Tên riêng khách hàng" },
                new() { LineNo = 2, FieldKey = "Note", FieldValue = "Dòng 1\nDòng 2 — giữ nguyên" },
                new() { LineNo = 3, FieldKey = "Legacy", FieldValue = "Inactive text", IsActive = false }
            ]
        }, default);
        Assert.True(created.Success, created.Message);
        var labelId = created.Data!.CustomerLabelHeaderId;
        db.ChangeTracker.Clear();
        var initial = await db.CustomerLabelHeaders.AsNoTracking().Include(h => h.Details).SingleAsync(h => h.Id == labelId);
        Assert.Equal("KH_375_1", initial.ColorCode);
        Assert.Null(await db.Products.Where(p => p.ProductId == product.ProductId).Select(p => p.ColourCode).SingleAsync());
        var noteId = initial.Details.Single(d => d.FieldKey == "Note").Id;
        var edited = await new PatchCustomerLabelDetailCommandHandler(db, sale).Handle(new() {
            CustomerLabelHeaderId = labelId, CustomerLabelDetailId = noteId,
            FieldValue = "Sale cập nhật trước khi Lab tạo mã\nGiữ dòng thứ hai"
        }, default);
        Assert.True(edited.Success, edited.Message);
        db.ChangeTracker.Clear();
        var baseline = await db.CustomerLabelHeaders.AsNoTracking().Include(h => h.Details).SingleAsync(h => h.Id == labelId);

        var sender = DispatchProxy.Create<ISender, Notifications>();
        var subjects = new SampleRequestConversationSubjectService(db);
        var quotationSync = new DraftQuotationProductSnapshotSyncService(db, new QuotationConversationSubjectService(db));
        var patch = new PatchSampleRequestCommandHandler(db, lab, Visibility.Create(lab), subjects, quotationSync,
            sender, new CurrentUserPermissionService(lab));
        async Task PatchCode(string code)
        {
            db.ChangeTracker.Clear();
            var token = await db.SampleRequests.Where(r => r.SampleRequestId == request.SampleRequestId).Select(r => r.UpdatedDate).SingleAsync();
            var result = await patch.Handle(new() { SampleRequestId = request.SampleRequestId, ColourCode = code, ExpectedUpdatedDate = token }, default);
            Assert.True(result.Success, result.Message);
        }
        async Task AssertStored(string code)
        {
            db.ChangeTracker.Clear();
            var stored = await db.CustomerLabelHeaders.AsNoTracking().Include(h => h.Details).SingleAsync(h => h.Id == labelId);
            Assert.Equal(code, stored.ColorCode); // Do not let projection fallback hide a missing persisted update.
            Assert.Equal(baseline.ProductId, stored.ProductId);
            Assert.Equal(baseline.CustomerId, stored.CustomerId);
            Assert.Equal(baseline.CreatedBy, stored.CreatedBy);
            Assert.Equal(baseline.CreatedDate, stored.CreatedDate);
            Assert.Equal(baseline.UpdatedDate, stored.UpdatedDate);
            Assert.Equal(baseline.Details.OrderBy(d => d.LineNo).Select(d => (d.Id, d.LineNo, d.FieldKey, d.FieldValue, d.IsActive)),
                stored.Details.OrderBy(d => d.LineNo).Select(d => (d.Id, d.LineNo, d.FieldKey, d.FieldValue, d.IsActive)));
            var dto = Assert.Single(await new GetCustomerLabelsQueryHandler(db, sale).Handle(new() {
                ProductId = product.ProductId, CustomerId = customer.CustomerId
            }, default));
            Assert.Equal(code, dto.ColorCode);
            Assert.Equal("Sale cập nhật trước khi Lab tạo mã\nGiữ dòng thứ hai", dto.Details.Single(d => d.Id == noteId).FieldValue);
            Assert.False(dto.Details.Single(d => d.FieldKey == "Legacy").IsActive);
        }

        await AssertStored("KH_375_1");
        await PatchCode("WPTEST001");
        await AssertStored("WPTEST001");

        db.ChangeTracker.Clear();
        var change = await new ChangeSampleRequestColourCodeCommandHandler(db, db, lab, Visibility.Create(lab),
            subjects, quotationSync, sender).Handle(new() {
                SampleRequestId = request.SampleRequestId, ColourCode = "WPTEST002", IdempotencyKey = Guid.NewGuid(),
                ExpectedUpdatedDate = await db.SampleRequests.Where(r => r.SampleRequestId == request.SampleRequestId).Select(r => r.UpdatedDate).SingleAsync()
            }, default);
        Assert.True(change.Success, change.Message);
        await AssertStored("WPTEST002");

        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            await PatchCode("ROLLBACK003");
            Assert.Equal("ROLLBACK003", await db.CustomerLabelHeaders.AsNoTracking().Where(h => h.Id == labelId).Select(h => h.ColorCode).SingleAsync());
            await transaction.RollbackAsync();
        }
        await AssertStored("WPTEST002");
    }

    private sealed class Clock(DateTime now) : IDateTimeProvider { public DateTime Now => now; }
    public class Notifications : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            Assert.IsType<CreateSampleRequestDirectPatchNotificationCommand>(args![0]);
            return Task.FromResult(OperationResult<SendInternalMessageResultDto>.Ok(new SendInternalMessageResultDto()));
        }
    }
}

