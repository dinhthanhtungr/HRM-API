using HRM.Application.Features.DevAndQA.LabTests.Dtos;
using MediatR;

namespace HRM.Application.Features.DevAndQA.LabTests.Commands.ImportLabTests;

/// <summary>ImportLabTests: thao tác phiếu test/bảng chung trong company hiện tại.</summary>
public sealed record ImportLabTestsCommand(Guid ImportBatchId, IReadOnlyList<LabTestImportRow> Rows) : IRequest<LabTestImportResultDto>;
