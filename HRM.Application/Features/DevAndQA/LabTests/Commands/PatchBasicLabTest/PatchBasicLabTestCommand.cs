using HRM.Application.Features.DevAndQA.LabTests.Dtos;
using HRM.Application.Commons.Pagination;
using MediatR;

namespace HRM.Application.Features.DevAndQA.LabTests.Commands.PatchBasicLabTest;

/// <summary>Phiếu test cơ bản theo company; không tạo Material hoặc voucher.</summary>
public sealed record PatchBasicLabTestCommand(Guid Id, BasicLabTestWriteRequest Data) : IRequest<BasicLabTestDto>;

