using HRM.Application.Features.DevAndQA.LabTests.Dtos;
using MediatR;

namespace HRM.Application.Features.DevAndQA.LabTests.Commands.CreateLabTest;

/// <summary>CreateLabTest: thao tác phiếu test/bảng chung trong company hiện tại.</summary>
public sealed record CreateLabTestCommand(LabTestWriteRequest Data) : IRequest<LabTestDto>;
