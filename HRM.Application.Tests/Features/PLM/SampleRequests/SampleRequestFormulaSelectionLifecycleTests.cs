using System.Reflection;
using HRM.Application.Features.PLM.SampleRequests.Commands.PatchSampleRequest;
using HRM.Domain.Entities.SampleRequestSchema;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleRequestFormulaSelectionLifecycleTests
{
    [Theory]
    [InlineData(2026, 10, 31)]
    [InlineData(2026, 11, 1)]
    public void SelectingFormulaDoesNotInventCustomerApprovalOrCompleteSentRequest(int year, int month, int day)
    {
        var originalReply = new DateTime(2026, 10, 30);
        var sample = new SampleRequest
        {
            SampleRequestId = Guid.NewGuid(), CreatedDate = new(year, month, day),
            Status = "SampleSent", ResponseDeliveryDate = originalReply
        };
        var command = new PatchSampleRequestCommand { FormulaId = Guid.NewGuid() };
        var handlerType = typeof(PatchSampleRequestCommand).Assembly.GetType(
            "HRM.Application.Features.PLM.SampleRequests.Commands.PatchSampleRequest.PatchSampleRequestCommandHandler")!;
        var method = handlerType.GetMethod("ApplyStatusRules", BindingFlags.NonPublic | BindingFlags.Static)!;

        var startedProcessing = (bool)method.Invoke(null, [sample, null, command])!;

        Assert.False(startedProcessing);
        Assert.Equal("SampleSent", sample.Status);
        Assert.Equal(originalReply, sample.ResponseDeliveryDate);
    }
}
