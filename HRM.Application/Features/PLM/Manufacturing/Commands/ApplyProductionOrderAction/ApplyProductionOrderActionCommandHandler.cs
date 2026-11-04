using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Manufacturing.Commands.UpdateProductionOrderInformation;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;
using HRM.Domain.Enums.Manufacturings;
using MediatR;

namespace HRM.Application.Features.PLM.Manufacturing.Commands.ApplyProductionOrderAction;

internal sealed class ApplyProductionOrderActionCommandHandler(ISender sender) : IRequestHandler<ApplyProductionOrderActionCommand, OperationResult>
{
    public Task<OperationResult> Handle(ApplyProductionOrderActionCommand command, CancellationToken ct)
    {
        if (!Enum.IsDefined(command.Action))
        {
            return Task.FromResult(OperationResult.Fail("Thao tác không hợp lệ."));
        }

        var r = command.Request;
        return sender.Send(new UpdateProductionOrderInformationCommand(
            command.Id,
            new UpdateProductionOrderInformationRequest
            {
                StepOfProduct = r.StepOfProduct,
                ExpectedDate = r.ExpectedDate,
                TotalQuantity = r.TotalQuantity,
                NumOfBatches = r.NumOfBatches,
                LabNote = r.LabNote,
                Requirement = r.Requirement,
                PlpuNote = r.PlpuNote,
                QcCheck = r.QcCheck
            },
            command.Action
        ), ct);
    }
}
