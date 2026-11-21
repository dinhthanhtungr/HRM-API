using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.CRM.CustomerCare.SampleSharingGroups;

public sealed record AssignCustomerSampleSharingGroupCommand(Guid CustomerId, AssignSampleSharingGroupRequest Request) : IRequest<OperationResult>;
