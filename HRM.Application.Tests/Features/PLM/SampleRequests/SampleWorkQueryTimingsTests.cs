using System.Diagnostics;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials;
using Microsoft.Extensions.Logging.Abstractions;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleWorkQueryTimingsTests
{
    [Fact]
    public void NamedDevelopmentTimingFlowsThroughAControllerChildActivity()
    {
        using var parent = new Activity("middleware").Start();
        parent.SetTag(SampleWorkDiagnosticTags.NamedTimingEnabled, true);
        using var child = new Activity("controller").Start();
        using (var timing = SampleWorkDiagnosticTags.Start("sample_page"))
        {
            Assert.NotNull(timing);
            timing.Mark("keys");
        }
        Assert.Contains("sample_page_keys;dur=", Assert.IsType<string>(child.GetTagItem(SampleWorkDiagnosticTags.ServerTiming)));
        Assert.Null(parent.GetTagItem(SampleWorkDiagnosticTags.ServerTiming));
    }

    [Fact]
    public void NestedActivitiesWithoutTheDevelopmentMarkerStayUnmeasured()
    {
        using var parent = new Activity("ordinary-request").Start();
        using var child = new Activity("controller").Start();
        Assert.Null(SampleWorkDiagnosticTags.Start("sample_page"));
        Assert.Null(child.GetTagItem(SampleWorkDiagnosticTags.ServerTiming));
    }

    [Fact]
    public void NestedPhasesAppendDurationOnlyToCurrentRequestActivity()
    {
        using var activity = new Activity("sample-daily-test").Start();
        using (var outer = new SampleWorkQueryTimings(NullLogger.Instance, true, "report"))
        {
            using (var inner = new SampleWorkQueryTimings(NullLogger.Instance, true))
            {
                inner.Mark("count");
                inner.Mark("payload");
            }
            outer.Mark("sample-query");
        }
        var header = Assert.IsType<string>(activity.GetTagItem(SampleWorkDiagnosticTags.ServerTiming));
        Assert.Matches(@"^samples_count;dur=\d+, samples_payload;dur=\d+, samples_finish;dur=\d+, report_sample-query;dur=\d+, report_finish;dur=\d+$", header);
    }

    [Fact]
    public void DisabledDiagnosticsDoNotAttachAnyRequestMetadata()
    {
        using var activity = new Activity("sample-report-test").Start();
        using (var timings = new SampleWorkQueryTimings(NullLogger.Instance, false))
            timings.Mark("payload");
        Assert.Null(activity.GetTagItem(SampleWorkDiagnosticTags.ServerTiming));
    }
}
