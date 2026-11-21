using System.Security.Claims;
using HRM.Api.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace HRM.Application.Tests.Diagnostics;

public sealed class PerformanceProbeTests
{
    [Fact]
    public void Default_keeps_all_hosted_services()
    {
        var services = Services();
        Assert.False(PerformanceProbe.Prepare(services, Config(false), new EnvironmentStub("Development")));
        Assert.Equal(3, services.Count);
    }

    [Fact]
    public void Opt_in_removes_application_workers_and_factories_only()
    {
        var services = Services();
        Assert.True(PerformanceProbe.Prepare(services, Config(true), new EnvironmentStub("Development")));
        Assert.Single(services);
        Assert.Equal(typeof(BackgroundService), services[0].ImplementationType);
    }

    [Fact]
    public void Cannot_enable_in_production()
    {
        Assert.Throws<InvalidOperationException>(() =>
            PerformanceProbe.Prepare(Services(), Config(true), new EnvironmentStub("Production")));
    }

    [Fact]
    public async Task Blocks_business_writes_without_invoking_pipeline()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/v1/plm/sample-requests";
        var called = false;
        await PerformanceProbe.InvokeAsync(context, _ => { called = true; return Task.CompletedTask; });
        Assert.False(called);
        Assert.Equal(405, context.Response.StatusCode);
    }

    [Theory]
    [InlineData("GET", "/api/v1/plm/sample-requests/reports/daily")]
    [InlineData("POST", "/api/v1/auth/login")]
    [InlineData("POST", "/api/v1/auth/refresh-token")]
    public async Task Allowed_requests_retain_the_original_authentication_pipeline(string method, string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        var principal = new ClaimsPrincipal(new ClaimsIdentity());
        context.User = principal;
        var called = false;
        await PerformanceProbe.InvokeAsync(context, next =>
        {
            called = true;
            Assert.Same(principal, next.User);
            Assert.False(next.User.Identity!.IsAuthenticated);
            next.Response.StatusCode = 401;
            return Task.CompletedTask;
        });
        Assert.True(called);
        Assert.Equal(401, context.Response.StatusCode);
    }

    private static ServiceCollection Services()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostedService, Worker>();
        services.AddSingleton<IHostedService>(_ => new Worker());
        services.AddSingleton(typeof(IHostedService), typeof(BackgroundService));
        return services;
    }

    private static IConfiguration Config(bool enabled) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { [PerformanceProbe.ConfigurationKey] = enabled.ToString() })
        .Build();

    private sealed class Worker : BackgroundService
    {
        protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
    }

    private sealed class EnvironmentStub(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "HRM.Api";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
