using HRM.Application.Features.PLM.MaterialProcessing.Dtos;
using MediatR;
namespace HRM.Application.Features.PLM.MaterialProcessing.Commands.PatchMaterialProcessing;
public sealed record PatchMaterialProcessingCommand(Guid Id, PatchMaterialProcessingRequest Request) : IRequest<MaterialProcessingResult>;
