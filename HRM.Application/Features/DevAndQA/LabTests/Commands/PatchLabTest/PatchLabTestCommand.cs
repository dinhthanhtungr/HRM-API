using HRM.Application.Features.DevAndQA.LabTests.Dtos;
using MediatR;

namespace HRM.Application.Features.DevAndQA.LabTests.Commands.PatchLabTest;

/// <summary>PatchLabTest: thao tác phiếu test/bảng chung trong company hiện tại.</summary>
public sealed record PatchLabTestCommand(Guid Id, LabTestWriteRequest Data) : IRequest<LabTestDto>;
