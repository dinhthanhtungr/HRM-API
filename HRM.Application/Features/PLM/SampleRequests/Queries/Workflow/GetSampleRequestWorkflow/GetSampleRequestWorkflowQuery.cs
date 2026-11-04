using HRM.Application.Features.PLM.SampleRequests.Dtos.Reports;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestWorkflow;

public sealed record GetSampleRequestWorkflowQuery(Guid SampleRequestId) : IRequest<SampleRequestWorkflowDto?>;
