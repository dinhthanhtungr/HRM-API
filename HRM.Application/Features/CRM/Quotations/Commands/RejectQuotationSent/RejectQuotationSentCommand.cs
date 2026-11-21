using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.Quotations.Dtos;
using MediatR;

namespace HRM.Application.Features.CRM.Quotations.Commands.RejectQuotationSent;

public sealed record RejectQuotationSentCommand(Guid QuotationId, RejectQuotationSentRequest Request) : IRequest<OperationResult>;
