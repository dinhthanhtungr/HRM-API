using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using HRM.Api.Controllers.Development;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.Development.SampleWorkflowSimulation;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleWorkflowSimulationHttpTests
{
    [Fact]
    public async Task RealControllerSupportsDeveloperSimulationWithoutAnyDatabaseRegistration()
    {
        var browser = Environment.GetEnvironmentVariable("HRM_SIMULATOR_BROWSER_QA") == "1";
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing", Args = [] });
        builder.Configuration.Sources.Clear(); builder.Configuration.AddInMemoryCollection(); builder.Logging.ClearProviders();
        builder.WebHost.UseUrls(browser ? "http://127.0.0.1:5319" : "http://127.0.0.1:0");
        builder.Services.AddMediatR(typeof(GetSimulationScenariosQuery).Assembly);
        builder.Services.AddHttpContextAccessor(); builder.Services.AddScoped<ICurrentUser, TestUser>();
        builder.Services.AddAuthentication("SimulatorTest").AddScheme<AuthenticationSchemeOptions, TestAuth>("SimulatorTest", _ => { });
        builder.Services.AddAuthorization(); builder.Services.AddControllers().AddApplicationPart(typeof(SampleWorkflowSimulationController).Assembly);
        builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins("http://127.0.0.1:4319").AllowAnyHeader().AllowAnyMethod()));
        await using var app = builder.Build();
        app.UseCors(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
        var stop = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (browser) app.MapPost("/qa/stop", () => { stop.TrySetResult(); return Results.Ok(); });
        await app.StartAsync();
        try {
            using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
            const string route = "/api/v1/dev/sample-workflow-simulator";
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(route)).StatusCode);
            client.DefaultRequestHeaders.Add("X-Simulator-Test-Role", "SaleUser");
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(route)).StatusCode);
            client.DefaultRequestHeaders.Remove("X-Simulator-Test-Role"); client.DefaultRequestHeaders.Add("X-Simulator-Test-Role", "Developer");
            var cases = await client.GetFromJsonAsync<SimulationScenario[]>(route); Assert.Equal(SimulationCatalog.All().Count, cases!.Length);
            Assert.DoesNotContain(cases, item => item.Id == "price-readonly");
            var response = await client.PostAsJsonAsync(route + "/evaluate", new SimulationStepRequest());
            response.EnsureSuccessStatusCode(); var view = await response.Content.ReadFromJsonAsync<SimulationView>();
            Assert.Equal("quote-new", view!.ScenarioId); Assert.Equal("ApproveFormula", view.WorkCode);
            Assert.DoesNotContain(view.Actions, a => a.Id == "RequestKg");
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(route + "/evaluate", new SimulationStepRequest { ActionId = "Complete" })).StatusCode);
            var batchResponse = await client.PostAsJsonAsync(route + "/evaluate", new SimulationStepRequest {
                ScenarioId = "feedback", ActionId = "SampleFeedback:1", Input = new() {
                    Feedbacks = [new("option-1-2", "Approved", "2026-11-15", "Chosen from the complete batch")] } });
            batchResponse.EnsureSuccessStatusCode();
            var saved = (await batchResponse.Content.ReadFromJsonAsync<SimulationView>())!;
            Assert.Single(saved.State.Batches[0].Options, option => option.Result == "Approved");
            Assert.Equal(2, saved.State.Batches[0].Options.Count(option => option.Result == "Failed"));
            Assert.Single(saved.State.History);
            Assert.Equal("Completed", saved.State.Status);
            Assert.DoesNotContain(saved.Actions, action => action.Id == "Complete");
            if (browser) { Console.WriteLine("SIMULATOR_QA_READY http://127.0.0.1:5319; NO DB REGISTERED"); await stop.Task.WaitAsync(TimeSpan.FromMinutes(25)); }
        }
        finally { await app.StopAsync(); }
    }
    public sealed class TestUser(IHttpContextAccessor accessor) : ICurrentUser
    {
        public bool IsAuthenticated => accessor.HttpContext!.User.Identity?.IsAuthenticated == true;
        public Guid UserId => Guid.Parse("10000000-0000-0000-0000-000000000001");
        public Guid? CompanyId => UserId;
        public Guid? EmployeeId => UserId;
        public string? UserName => "synthetic-simulator";
        public string? Email => null;
        public IReadOnlyCollection<string> Roles => accessor.HttpContext!.User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray();
        public bool IsInRole(string role) => accessor.HttpContext!.User.IsInRole(role);
    }
    public sealed class TestAuth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role = Request.Headers["X-Simulator-Test-Role"].ToString();
            return Task.FromResult(role is "Developer" or "SaleUser"
                ? AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], Scheme.Name)), Scheme.Name))
                : AuthenticateResult.NoResult());
        }
    }
}
