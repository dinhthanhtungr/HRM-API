using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.PLM.SaleOrders.Commands.ConfirmStandardSample;

public sealed record ConfirmStandardSampleCommand(Guid OrderId, Guid DetailId) : IRequest<OperationResult>;
