using HRM.Application.Features.PLM.MaterialProcessing.Dtos;
using MediatR;
namespace HRM.Application.Features.PLM.MaterialProcessing.Commands.ConfirmMaterialProcessing;
public sealed record ConfirmMaterialProcessingCommand(Guid Id, ConfirmMaterialProcessingRequest Request) : IRequest<MaterialProcessingResult>;
