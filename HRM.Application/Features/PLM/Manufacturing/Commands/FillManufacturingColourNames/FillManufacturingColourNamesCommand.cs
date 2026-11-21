using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.PLM.Manufacturing.Commands.FillManufacturingColourNames;

/// <summary>Chỉ bổ sung snapshot màu MFG đang NULL từ Product cùng công ty; mặc định không ghi.</summary>
public sealed record FillManufacturingColourNamesCommand(bool DryRun = true)
    : IRequest<OperationResult<FillManufacturingColourNamesResult>>;

public sealed record ManufacturingColourNameCandidate(Guid OrderId, string ExternalId, Guid ProductId,
    string? ProductNameSnapshot, string? ProposedColourName);

public sealed record FillManufacturingColourNamesResult(bool DryRun, int Examined, int Matched, int Updated,
    int ConcurrentlySkipped, IReadOnlyList<ManufacturingColourNameCandidate> Items, bool HasMoreItems);
