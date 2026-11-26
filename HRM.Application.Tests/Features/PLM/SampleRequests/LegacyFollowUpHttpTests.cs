using System.Net.Http.Json;
using HRM.Api.Controllers.PLM;
using HRM.Api.Security.Authorization;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Concurrency;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Application.Features.PLM.Formulas.Services;
using HRM.Application.Features.PLM.SampleRequests.Commands.SendSampleRequestMessage;
using HRM.Application.Features.PLM.SampleRequests.DeliveryOptions;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using HRM.Application.Tests.Features.InternalMail.GroupStorage;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using static HRM.Application.Tests.Features.PLM.SampleRequests.SampleWorkDecisionPostgresTests;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

/// <summary>Real HTTP/controller/handlers with fresh, guarded PostgreSQL; no production startup or workers.</summary>
public sealed class LegacyFollowUpHttpTests(IsolatedGroupStoragePostgres postgres) : IClassFixture<IsolatedGroupStoragePostgres>
{
    [PostgresGroupStorageFact]
    public async Task TraceThreeLegacyFollowUpFlowsOverHttp()
    {
        var now = DateTime.Now.AddMinutes(-3);
        now = now.AddTicks(-(now.Ticks % 10));
        var old = new DateTime(2026, 10, 31, 8, 0, 0);
        var ids = new Dictionary<string, (Guid Request, Guid Formula)>();
        await using (var connection = await postgres.OpenAsync())
        await using (var db = Context(connection))
        {
            await SampleWorkflowMutationTestSupport.Schema(connection, db);
            foreach (var name in new[] { "ApplicationUser", "ApplicationRole", "ApplicationUserRole" })
            {
                var entity = db.Model.GetEntityTypes().Single(e => e.ClrType.Name == name);
                var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
                var schema = table.Schema ?? "public";
                var columns = entity.GetProperties().Select(p => $"\"{p.GetColumnName(table)}\" {p.GetRelationalTypeMapping().StoreType} NULL");
                // Trusted EF metadata, only on the fixture's guarded disposable cluster; composite identity key is supported.
                await using var ddl = new NpgsqlCommand($"CREATE SCHEMA IF NOT EXISTS \"{schema}\"; CREATE TABLE \"{schema}\".\"{table.Name}\" ({string.Join(',', columns)})", connection);
                await ddl.ExecuteNonQueryAsync();
            }
            async Task Row(string name, Dictionary<string, object?> values) => await Insert(connection, db.Model.GetEntityTypes().Single(e => e.ClrType.Name == name), values);
            foreach (var actor in new[] { SampleDeliveryBrowserApiTests.Lab, SampleDeliveryBrowserApiTests.Sale })
                await Row("Employee", new() { ["EmployeeId"] = actor, ["CompanyId"] = SampleDeliveryBrowserApiTests.Company, ["IsActive"] = true });
            var president = Guid.NewGuid(); var user = Guid.NewGuid(); var role = Guid.NewGuid();
            await Row("Employee", new() { ["EmployeeId"] = president, ["CompanyId"] = SampleDeliveryBrowserApiTests.Company, ["IsActive"] = true });
            db.Add(new HRM.Domain.Identity.ApplicationUser { Id = user, EmployeeId = president });
            db.Add(new HRM.Domain.Identity.ApplicationRole { Id = role, Name = ApplicationRoles.President });
            db.Add(new HRM.Domain.Identity.ApplicationUserRole { UserId = user, RoleId = role, IsActive = true });
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var customer = Guid.NewGuid(); var category = Guid.NewGuid();
            await Row("Customer", new() { ["CustomerId"] = customer, ["CompanyId"] = SampleDeliveryBrowserApiTests.Company, ["IsActive"] = true });
            await Row("Category", new() { ["CategoryId"] = category, ["CompanyId"] = SampleDeliveryBrowserApiTests.Company, ["IsActive"] = true });
            foreach (var action in new[] { "RequestChip", "RequestSample", "RequestRequote" })
            {
                var product = Guid.NewGuid(); var request = Guid.NewGuid(); var formula = Guid.NewGuid(); ids[action] = (request, formula);
                await Row("Product", new() { ["ProductId"] = product, ["CategoryId"] = category, ["CompanyId"] = SampleDeliveryBrowserApiTests.Company, ["IsActive"] = true, ["Name"] = action, ["ColourCode"] = "QA_" + action });
                await Row("Formula", new() { ["FormulaId"] = formula, ["ProductId"] = product, ["CompanyId"] = SampleDeliveryBrowserApiTests.Company, ["IsActive"] = true, ["Status"] = "Approved", ["ExternalId"] = "QA_VU_" + action, ["CreatedDate"] = old, ["UpdatedDate"] = now });
                await Row("SampleRequest", new() { ["SampleRequestId"] = request, ["ProductId"] = product, ["FormulaId"] = formula, ["CustomerId"] = customer, ["ManagerBy"] = SampleDeliveryBrowserApiTests.Sale, ["CompanyId"] = SampleDeliveryBrowserApiTests.Company, ["IsActive"] = true, ["Status"] = "Completed", ["InfoType"] = "Quotation", ["CreatedDate"] = old, ["UpdatedDate"] = now, ["ExternalId"] = "QA_TP_" + action });
                // Historical formula confirmation exists, as in a migrated TP with valid prior work.
                await Row("SampleRequestWorkflowEvent", new() { ["Id"] = Guid.NewGuid(), ["CompanyId"] = SampleDeliveryBrowserApiTests.Company, ["SampleRequestId"] = request, ["FormulaId"] = formula, ["EventType"] = "LabFormulaConfirmed", ["RecordedAt"] = now.AddSeconds(-1) });
            }
        }
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing", Args = [] });
        builder.Configuration.Sources.Clear(); builder.Configuration.AddInMemoryCollection(); builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddAuthentication("QA").AddScheme<AuthenticationSchemeOptions, SampleDeliveryBrowserApiTests.QaAuth>("QA", _ => { });
        builder.Services.AddAuthorization(o => {
            o.AddPolicy(SensitiveDataAuthorization.PolicyName, p => p.RequireUnrestrictedAccount());
            o.AddPolicy(HRM.Application.Commons.Authorization.PLM.PlmPolicies.ManageFormula, p => p.RequireAuthenticatedUser());
        });
        builder.Services.AddControllers().AddApplicationPart(typeof(SampleRequestsController).Assembly);
        builder.Services.AddMediatR(typeof(GetSampleDeliveriesQuery).Assembly);
        builder.Services.AddScoped(_ => postgres.OpenAsync().GetAwaiter().GetResult());
        builder.Services.AddScoped(s => Context(s.GetRequiredService<NpgsqlConnection>()));
        builder.Services.AddScoped<IPLMReadDbContext>(s => s.GetRequiredService<ApplicationDbContext>());
        builder.Services.AddScoped<IPLMWriteDbContext>(s => s.GetRequiredService<ApplicationDbContext>());
        builder.Services.AddScoped<ICRMReadDbContext>(s => s.GetRequiredService<ApplicationDbContext>());
        builder.Services.AddScoped<ICRMWriteDbContext>(s => s.GetRequiredService<ApplicationDbContext>());
        builder.Services.AddScoped<ICurrentUser, SampleDeliveryBrowserApiTests.QaUser>();
        builder.Services.AddScoped<ICurrentUserPermissionService, CurrentUserPermissionService>();
        builder.Services.AddScoped<ICustomerVisibilityService>(s => SampleDeliveryBrowserApiTests.QaVisibility.Create(s.GetRequiredService<ICurrentUser>()));
        builder.Services.AddSingleton<IDateTimeProvider, SampleDeliveryBrowserApiTests.QaClock>();
        builder.Services.AddSingleton(new KeyedMutationLock<Guid>());
        builder.Services.AddScoped<FormulaVersionService>();
        builder.Services.AddScoped(s => new FormulaWriteService(s.GetRequiredService<IPLMWriteDbContext>(), null!));
        builder.Services.AddScoped<FormulaPricingReviewService>(); builder.Services.AddScoped<LegacySampleDeliveryService>();
        builder.Services.AddScoped<SampleDeliveryCompletion>();
        // Stop only outbound communication; workflow, quote selection and persistence handlers stay real.
        builder.Services.AddScoped<IRequestHandler<SendSampleRequestMessageCommand, OperationResult<SendInternalMessageResultDto>>, MailSink>();
        await using var app = builder.Build();
        app.UseAuthentication(); app.UseAuthorization(); app.MapControllers(); await app.StartAsync();
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
            var anonymous = await client.PostAsJsonAsync($"/api/v1/plm/sample-requests/{ids["RequestChip"].Request}/workflow/actions", new { action = "RequestChip" });
            Assert.Equal(System.Net.HttpStatusCode.Unauthorized, anonymous.StatusCode);
            foreach (var (action, id) in ids)
            {
                client.DefaultRequestHeaders.Remove("X-QA-Role"); client.DefaultRequestHeaders.Add("X-QA-Role", "SaleUser");
                var route = $"/api/v1/plm/sample-requests/{id.Request}";
                var plan = await client.PostAsJsonAsync(route + "/workflow/actions", new { action, requestedKg = 2, requestedSets = 1, note = "Synthetic legacy follow-up", expectedUpdatedDate = now, expectedFormulaUpdatedDate = now });
                Console.WriteLine($"TRACE {action} PLAN {(int)plan.StatusCode}: {await plan.Content.ReadAsStringAsync()}");
                Assert.True(plan.IsSuccessStatusCode);
                await using var connection = await postgres.OpenAsync(); await using var db = Context(connection);
                var parent = await db.SampleRequests.AsNoTracking().SingleAsync(r => r.SampleRequestId == id.Request);
                Assert.Equal(old, parent.CreatedDate); Assert.Equal("InProgress", parent.Status);
                Assert.True(await db.SampleRequestWorkflowEvents.AnyAsync(e => e.SampleRequestId == id.Request && e.EventType == SampleFollowUpRules.Started));
                client.DefaultRequestHeaders.Remove("X-QA-Role"); client.DefaultRequestHeaders.Add("X-QA-Role", "LabUser");
                HttpResponseMessage? sent = null;
                var batch = Guid.NewGuid();
                if (action == "RequestSample")
                    sent = await client.PostAsJsonAsync(route + "/deliveries", new { batchId = batch, sentDate = now, expectedRequestUpdatedDate = parent.UpdatedDate, options = new[] { new { formulaId = id.Formula, quantityKg = 2, expectedUpdatedDate = now } } });
                if (action == "RequestChip")
                    sent = await client.PatchAsJsonAsync($"/api/v1/plm/formulas/{id.Formula}/status", new { sampleRequestId = id.Request, status = 3, expectedUpdatedDate = now });
                if (sent is not null) Console.WriteLine($"TRACE {action} SEND {(int)sent.StatusCode}: {await sent.Content.ReadAsStringAsync()}");
                if (sent is not null) Assert.True(sent.IsSuccessStatusCode);
                if (action == "RequestChip")
                {
                    var trial = await db.SampleRequestSampleTrials.AsNoTracking().SingleAsync(t => t.SampleRequestId == id.Request);
                    client.DefaultRequestHeaders.Remove("X-QA-Role"); client.DefaultRequestHeaders.Add("X-QA-Role", "SaleUser");
                    var chipReply = await client.PostAsJsonAsync(route + "/customer-feedback", new { status = "Approved", customerReplyStatus = "APPROVED", customerReplyNote = "Synthetic chip accepted", customerReplyDate = now, expectedUpdatedDate = trial.UpdatedDate });
                    Console.WriteLine($"TRACE {action} FEEDBACK {(int)chipReply.StatusCode}: {await chipReply.Content.ReadAsStringAsync()}");
                    Assert.True(chipReply.IsSuccessStatusCode);
                }
                if (action == "RequestSample" && sent?.IsSuccessStatusCode == true)
                {
                    var trial = await db.SampleRequestSampleTrials.AsNoTracking().SingleAsync(t => t.SampleRequestId == id.Request);
                    parent = await db.SampleRequests.AsNoTracking().SingleAsync(r => r.SampleRequestId == id.Request);
                    client.DefaultRequestHeaders.Remove("X-QA-Role"); client.DefaultRequestHeaders.Add("X-QA-Role", "SaleUser");
                    var feedback = await client.PostAsJsonAsync(route + $"/deliveries/{batch}/feedback-batch", new { operationId = Guid.NewGuid(), expectedRequestUpdatedDate = parent.UpdatedDate, options = new[] { new { operationId = Guid.NewGuid(), trialId = trial.SampleRequestSampleTrialId, approved = true, replyDate = now, note = "Synthetic accepted", expectedRequestUpdatedDate = parent.UpdatedDate, expectedTrialUpdatedDate = trial.UpdatedDate } } });
                    Console.WriteLine($"TRACE {action} FEEDBACK {(int)feedback.StatusCode}: {await feedback.Content.ReadAsStringAsync()}");
                    Assert.True(feedback.IsSuccessStatusCode);
                }
                parent = await db.SampleRequests.AsNoTracking().SingleAsync(r => r.SampleRequestId == id.Request);
                Console.WriteLine($"TRACE {action} DB status={parent.Status}; trials={await db.SampleRequestSampleTrials.CountAsync(t => t.SampleRequestId == id.Request)}; events={await db.SampleRequestWorkflowEvents.CountAsync(e => e.SampleRequestId == id.Request)}");
                if (action == "RequestSample") { Assert.Equal("Completed", parent.Status); Assert.Equal(id.Formula, parent.FormulaId); }
                if (action == "RequestRequote")
                {
                    // Existing quote date belongs to the synthetic old TP; approval/publication is a separate pricing feature.
                    await db.SampleRequests.Where(r => r.SampleRequestId == id.Request).ExecuteUpdateAsync(s => s.SetProperty(r => r.RealPriceQuoteDate, now));
                    client.DefaultRequestHeaders.Remove("X-QA-Role"); client.DefaultRequestHeaders.Add("X-QA-Role", "SaleUser");
                    var priceReply = await client.PostAsJsonAsync(route + "/quotation-feedback", new { quotedAt = now, expectedUpdatedDate = parent.UpdatedDate, feedback = new { outcome = "NoResponse", occurredAt = now, note = "Synthetic awaiting customer", customerFeedbackNote = "Synthetic price feedback" } });
                    Console.WriteLine($"TRACE RequestRequote PRICE_FEEDBACK {(int)priceReply.StatusCode}: {await priceReply.Content.ReadAsStringAsync()}");
                    Assert.Equal(System.Net.HttpStatusCode.BadRequest, priceReply.StatusCode);
                    await Insert(connection, db.Model.GetEntityTypes().Single(e => e.ClrType.Name == "ProductPricingVersion"), new() {
                        ["ProductPricingVersionId"] = Guid.NewGuid(), ["CompanyId"] = SampleDeliveryBrowserApiTests.Company,
                        ["ProductId"] = parent.ProductId, ["SourceFormulaId"] = id.Formula, ["Version"] = 1,
                        ["Currency"] = "VND", ["Status"] = HRM.Domain.Enums.CustomerEnum.ProductPricingStatus.Approved,
                        ["StandardSellingPrice"] = 10000m, ["IsActive"] = true, ["CreatedDate"] = now, ["ApprovedAt"] = now });
                    var approvedReply = await client.PostAsJsonAsync(route + "/quotation-feedback", new { quotedAt = now, expectedUpdatedDate = parent.UpdatedDate, feedback = new { outcome = "NoResponse", occurredAt = now, note = "Synthetic awaiting customer", customerFeedbackNote = "Synthetic price feedback" } });
                    Console.WriteLine($"TRACE RequestRequote PRICE_FEEDBACK_WITH_APPROVED_PRICE {(int)approvedReply.StatusCode}: {await approvedReply.Content.ReadAsStringAsync()}");
                    Assert.True(approvedReply.IsSuccessStatusCode);
                    parent = await db.SampleRequests.AsNoTracking().SingleAsync(r => r.SampleRequestId == id.Request);
                    // Model a legacy Approved VU lacking imported confirmation evidence. Test data only.
                    await db.SampleRequestWorkflowEvents.Where(e => e.SampleRequestId == id.Request && e.EventType == "LabFormulaConfirmed").ExecuteDeleteAsync();
                    var beforeRejected = await db.SampleRequestWorkflowEvents.CountAsync(e => e.SampleRequestId == id.Request);
                    client.DefaultRequestHeaders.Remove("X-QA-Role"); client.DefaultRequestHeaders.Add("X-QA-Role", "SaleUser");
                    var missing = await client.PostAsJsonAsync(route + "/workflow/actions", new { action, note = "Synthetic no historical confirmation", expectedUpdatedDate = parent.UpdatedDate, expectedFormulaUpdatedDate = now });
                    Console.WriteLine($"TRACE RequestRequote MISSING_CONFIRMATION {(int)missing.StatusCode}: {await missing.Content.ReadAsStringAsync()}");
                    Assert.Equal(System.Net.HttpStatusCode.BadRequest, missing.StatusCode);
                    Assert.Equal(beforeRejected, await db.SampleRequestWorkflowEvents.CountAsync(e => e.SampleRequestId == id.Request));
                    Assert.Equal(parent.UpdatedDate, await db.SampleRequests.Where(r => r.SampleRequestId == id.Request).Select(r => r.UpdatedDate).SingleAsync());
                }
            }
        }
        finally { await app.StopAsync(); }
    }
    private static ApplicationDbContext Context(NpgsqlConnection connection) => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
    private sealed class MailSink : IRequestHandler<SendSampleRequestMessageCommand, OperationResult<SendInternalMessageResultDto>>
    {
        public Task<OperationResult<SendInternalMessageResultDto>> Handle(SendSampleRequestMessageCommand request, CancellationToken ct) => Task.FromResult(OperationResult<SendInternalMessageResultDto>.Ok(new SendInternalMessageResultDto()));
    }
}
