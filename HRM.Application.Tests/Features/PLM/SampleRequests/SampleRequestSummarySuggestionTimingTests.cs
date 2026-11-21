using System.Diagnostics;
using System.Reflection;
using HRM.Api.Controllers.PLM;
using HRM.Application.Features.PLM.SampleRequests.Dtos.Summary;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSummarySuggestions;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleRequestSummarySuggestionTimingTests
{
    [Theory]
    [InlineData("Development", true)]
    [InlineData("Production", false)]
    [InlineData("Staging", false)]
    public async Task TimingIsDevelopmentOnlyAndDoesNotIncludeUserContent(string environment, bool expected)
    {
        using var services = new ServiceCollection().AddSingleton<IHostEnvironment>(new Environment(environment))
            .BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        var controller = new SampleRequestsController(DispatchProxy.Create<ISender, Sender>(), null!)
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };
        var parent = Activity.Current;
        await controller.GetSummarySuggestions(new GetSampleRequestSummarySuggestionsQuery { Q = "untrusted-content" }, default);
        var header = context.Response.Headers["Server-Timing"].ToString();
        Assert.Equal(expected, header.Contains("summary_suggestions_total;dur="));
        Assert.Equal(expected, header.Contains("summary_suggestions_query;dur="));
        Assert.DoesNotContain("untrusted-content", header);
        Assert.DoesNotContain("desc=", header);
        Assert.Same(parent, Activity.Current);
        Assert.NotNull(typeof(SampleRequestsController).GetCustomAttribute<AuthorizeAttribute>());
    }

    public class Sender : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method?.Name != "Send" || args?[0] is not GetSampleRequestSummarySuggestionsQuery)
                throw new NotSupportedException();
            using var timings = SampleWorkDiagnosticTags.Start("summary_suggestions");
            timings?.Mark("scope"); timings?.Mark("query"); timings?.Mark("map");
            return Task.FromResult<IReadOnlyList<SampleRequestSummarySuggestionDto>>([]);
        }
    }

    private sealed class Environment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
