using HRM.Application.Features.DevAndQA.LabTests.Dtos;
using MediatR;

namespace HRM.Application.Features.DevAndQA.LabTests.Commands.DeleteLabTest;

/// <summary>DeleteLabTest: thao tác phiếu test/bảng chung trong company hiện tại.</summary>
public sealed record DeleteLabTestCommand(Guid Id) : IRequest<bool>;
