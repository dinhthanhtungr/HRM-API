using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.CRM.CustomerCare.SampleSharingGroups;

public sealed record CreateCustomerSampleSharingGroupCommand(CreateSampleSharingGroupRequest Request) : IRequest<OperationResult<Guid>>;
