using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Domain.Enums.SampleRequests;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.UpdateSampleRequestColourCode;

internal sealed class UpdateSampleRequestColourCodeCommandHandler
    : IRequestHandler<UpdateSampleRequestColourCodeCommand, OperationResult<string>>
{
    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public UpdateSampleRequestColourCodeCommandHandler(
        IPLMWriteDbContext dbContext,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<OperationResult<string>> Handle(
        UpdateSampleRequestColourCodeCommand request,
        CancellationToken cancellationToken)
    {
        if (request.SampleRequestId == Guid.Empty)
        {
            return OperationResult<string>.Fail("SampleRequestId is invalid.");
        }

        if (string.IsNullOrWhiteSpace(request.ColourCode))
        {
            return OperationResult<string>.Fail("ColourCode is required.");
        }

        var sampleRequest = await _dbContext.SampleRequests
            .Include(x => x.Product)
            .FirstOrDefaultAsync(
                x => x.SampleRequestId == request.SampleRequestId && x.IsActive,
                cancellationToken);

        if (sampleRequest is null)
        {
            return OperationResult<string>.Fail("Sample request was not found.");
        }

        var product = sampleRequest.Product;
        if (product is null || !product.IsActive)
        {
            return OperationResult<string>.Fail("Product does not exist or is inactive.");
        }

        var colourCodeResult = await SampleRequestColourCodeGenerator.ResolveAsync(
            _dbContext,
            request.ColourCode,
            excludedProductId: product.ProductId,
            currentColourCode: product.ColourCode,
            cancellationToken);

        if (!colourCodeResult.Success)
        {
            return OperationResult<string>.Fail(colourCodeResult.Message ?? "ColourCode is invalid.");
        }

        var resolvedColourCode = colourCodeResult.Data?.ColourCode;
        if (string.IsNullOrWhiteSpace(resolvedColourCode))
        {
            return OperationResult<string>.Fail("ColourCode is invalid.");
        }

        var duplicated = await _dbContext.Products
            .AsNoTracking()
            .AnyAsync(x =>
                x.ProductId != product.ProductId &&
                x.ColourCode == resolvedColourCode &&
                x.IsActive,
                cancellationToken);

        if (duplicated)
        {
            return OperationResult<string>.Fail("ColourCode already exists.");
        }

        var now = DateTime.Now;
        var employeeId = _currentUser.EmployeeId;

        product.ColourCode = resolvedColourCode;

        if (!string.IsNullOrWhiteSpace(colourCodeResult.Data?.AdditiveCode))
        {
            product.Additive = colourCodeResult.Data.AdditiveCode;
        }

        if (employeeId.HasValue && !product.CreatedBy.HasValue)
        {
            product.CreatedBy = employeeId.Value;
            product.CreatedDate = now;
        }

        if (employeeId.HasValue)
        {
            product.UpdatedBy = employeeId.Value;
            product.UpdatedDate = now;
            sampleRequest.UpdatedBy = employeeId.Value;
        }

        if (IsStatus(sampleRequest.Status, SampleRequestStatus.New) &&
            !string.IsNullOrWhiteSpace(product.Name))
        {
            sampleRequest.Status = SampleRequestStatus.InProgress.ToString();
        }

        sampleRequest.UpdatedDate = now;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return OperationResult<string>.Ok(resolvedColourCode, "Updated colour code successfully.");
    }

    private static bool IsStatus(string? currentStatus, SampleRequestStatus expectedStatus)
    {
        return string.Equals(
            currentStatus?.Trim(),
            expectedStatus.ToString(),
            StringComparison.OrdinalIgnoreCase);
    }
}
