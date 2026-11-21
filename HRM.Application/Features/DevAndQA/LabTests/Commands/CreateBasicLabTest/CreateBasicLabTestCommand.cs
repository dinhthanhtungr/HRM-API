using HRM.Application.Features.DevAndQA.LabTests.Dtos;
using HRM.Application.Commons.Pagination;
using MediatR;

namespace HRM.Application.Features.DevAndQA.LabTests.Commands.CreateBasicLabTest;

/// <summary>Phiếu test cơ bản theo company; không tạo Material hoặc voucher.</summary>
public sealed record CreateBasicLabTestCommand(BasicLabTestWriteRequest Data) : IRequest<BasicLabTestDto>;

