using HRM.Application.Features.DevAndQA.LabTests.Dtos;
using MediatR;

namespace HRM.Application.Features.DevAndQA.LabTests.Queries.GetLabTest;

/// <summary>GetLabTest: thao tác phiếu test/bảng chung trong company hiện tại.</summary>
public sealed record GetLabTestQuery(Guid Id) : IRequest<LabTestDto>;
