using HRM.Application.Abstractions.Commons.ExternalIds;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.SampleRequests.Commands.SendSampleRequestMessage;
using HRM.Domain.Entities.AttachmentSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.Category;
using HRM.Domain.Enums.SampleRequests;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.CreateSampleRequest;

internal sealed class CreateSampleRequestCommandHandler
    : IRequestHandler<CreateSampleRequestCommand, OperationResult<Guid>>
{
    private const int MaxInitialLabMessageLength = 2000;

    private readonly IPLMWriteDbContext _dbContext;
    private readonly IExternalIdService _externalIdService;
    private readonly ICurrentUser _currentUser;
    private readonly ISender _sender;

    public CreateSampleRequestCommandHandler(
        IPLMWriteDbContext dbContext,
        IExternalIdService externalIdService,
        ICurrentUser currentUser,
        ISender sender)
    {
        _dbContext = dbContext;
        _externalIdService = externalIdService;
        _currentUser = currentUser;
        _sender = sender;
    }

    public async Task<OperationResult<Guid>> Handle(
        CreateSampleRequestCommand request,
        CancellationToken cancellationToken)
    {
        if (request.CompanyId == Guid.Empty)
        {
            return OperationResult<Guid>.Fail("CompanyId is invalid.");
        }

        if (request.CustomerId == Guid.Empty)
        {
            return OperationResult<Guid>.Fail("CustomerId is invalid.");
        }

        var currentUserId = _currentUser.EmployeeId.GetValueOrDefault();
        if (currentUserId == Guid.Empty)
        {
            return OperationResult<Guid>.Fail("Current employee is invalid.");
        }

        var initialLabMessage = TrimToNull(request.InitialLabMessage);
        if (initialLabMessage is { Length: > MaxInitialLabMessageLength })
        {
            return OperationResult<Guid>.Fail($"InitialLabMessage cannot exceed {MaxInitialLabMessageLength} characters.");
        }

        var customerExists = await _dbContext.Customers
            .AsNoTracking()
            .AnyAsync(x =>
                x.CustomerId == request.CustomerId &&
                x.CompanyId == request.CompanyId &&
                x.IsActive == true,
                cancellationToken);

        if (!customerExists)
        {
            return OperationResult<Guid>.Fail("Customer does not exist or is inactive.");
        }

        var managerByResult = await ResolveManagerByAsync(
            request.CustomerId,
            request.CompanyId,
            currentUserId,
            cancellationToken);

        if (!managerByResult.Success)
        {
            return OperationResult<Guid>.Fail(managerByResult.Message ?? "ManagerBy is invalid.");
        }

        var productResult = await ResolveProductIdAsync(
            request,
            cancellationToken);

        if (!productResult.Success)
        {
            return OperationResult<Guid>.Fail(productResult.Message ?? "Product is invalid.");
        }

        var externalId = await _externalIdService.GenerateGlobalCodeAsync(
            request.CompanyId,
            DocumentPrefix.TP.ToString(),
            cancellationToken);

        var exists = await _dbContext.SampleRequests
            .AsNoTracking()
            .AnyAsync(x =>
                x.CompanyId == request.CompanyId &&
                x.ExternalId == externalId &&
                x.IsActive,
                cancellationToken);

        if (exists)
        {
            return OperationResult<Guid>.Fail($"Sample request {externalId} already exists.");
        }

        var productId = productResult.Data;

        if (request.FormulaId.HasValue)
        {
            var formulaExists = await _dbContext.Formulas
                .AsNoTracking()
                .AnyAsync(x =>
                    x.FormulaId == request.FormulaId.Value &&
                    x.ProductId == productId &&
                    x.IsActive,
                    cancellationToken);

            if (!formulaExists)
            {
                return OperationResult<Guid>.Fail("Formula does not exist or is inactive.");
            }
        }

        var sampleRequestId = Guid.CreateVersion7();
        var attachmentCollectionId = Guid.CreateVersion7();

        await _dbContext.AttachmentCollections.AddAsync(new AttachmentCollection
        {
            AttachmentCollectionId = attachmentCollectionId
        }, cancellationToken);

        await _dbContext.SampleRequests.AddAsync(new SampleRequest
        {
            SampleRequestId = sampleRequestId,
            ExternalId = externalId,
            CustomerId = request.CustomerId,
            ManagerBy = managerByResult.Data,
            ProductId = productId,
            AttachmentCollectionId = attachmentCollectionId,
            FormulaId = request.FormulaId,
            BranchId = request.BranchId,
            CompanyId = request.CompanyId,
            Status = SampleRequestStatus.New.ToString(),
            RequestType = request.RequestType.Trim(),
            ExpectedQuantity = request.ExpectedQuantity,
            ExpectedPrice = request.ExpectedPrice,
            SampleQuantity = request.SampleQuantity,
            NumberDeliverySampleDate = request.NumberDeliverySampleDate,
            Package = request.Package.Trim(),
            BagWeight = request.BagWeight,
            CustomerProductCode = TrimToNull(request.CustomerProductCode),
            RequestDeliveryDate = request.RequestDeliveryDate,
            ExpectedDeliveryDate = request.ExpectedDeliveryDate,
            RequestTestSampleDate = request.RequestTestSampleDate,
            ExpectedPriceQuoteDate = request.ExpectedPriceQuoteDate,
            InfoType = TrimToNull(request.InfoType),
            OtherComment = TrimToNull(request.OtherComment),
            SaleComment = TrimToNull(request.SaleComment),
            AdditionalComment = TrimToNull(request.AdditionalComment),
            CreatedBy = currentUserId,
            CreatedDate = DateTime.Now,
            UpdatedBy = currentUserId,
            UpdatedDate = DateTime.Now,
            IsActive = true
        }, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        var messageResult = await _sender.Send(new SendSampleRequestMessageCommand
        {
            SampleRequestId = sampleRequestId,
            Type = SampleRequestNotificationType.GeneralMessage,
            Message = initialLabMessage ?? BuildLabSummaryMessage(externalId, request),
            TitleOverride = "Yêu cầu phối mẫu mới",
            ExtraRecipientEmployeeIds = request.InitialLabRecipientEmployeeIds
        }, cancellationToken);

        if (!messageResult.Success)
        {
            return OperationResult<Guid>.Ok(
                sampleRequestId,
                $"Created sample request successfully, but could not send Lab summary message: {messageResult.Message}");
        }

        return OperationResult<Guid>.Ok(sampleRequestId, "Created sample request successfully.");
    }




    // ============================================================= Helper Methods =============================================================
    private static string? TrimToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string BuildLabSummaryMessage(
        string externalId,
        CreateSampleRequestCommand request)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Yêu cầu phối mẫu mới {externalId}");
        Append(builder, "Loại yêu cầu", request.RequestType);
        Append(builder, "Số lượng dự kiến", request.ExpectedQuantity);
        Append(builder, "Giá dự kiến", request.ExpectedPrice);
        Append(builder, "Số lượng mẫu", request.SampleQuantity);
        Append(builder, "Số ngày giao mẫu", request.NumberDeliverySampleDate);
        Append(builder, "Quy cách đóng gói", request.Package);
        Append(builder, "Trọng lượng bao", request.BagWeight);
        Append(builder, "Mã sản phẩm KH", request.CustomerProductCode);
        Append(builder, "Ngày yêu cầu giao", request.RequestDeliveryDate);
        Append(builder, "Ngày giao dự kiến", request.ExpectedDeliveryDate);
        Append(builder, "Ngày yêu cầu test mẫu", request.RequestTestSampleDate);
        Append(builder, "Ngày dự kiến báo giá", request.ExpectedPriceQuoteDate);
        Append(builder, "Loại thông tin", request.InfoType);
        Append(builder, "Ghi chú Sale", request.SaleComment);
        Append(builder, "Ghi chú khác", request.OtherComment);
        Append(builder, "Ghi chú bổ sung", request.AdditionalComment);

        Append(builder, "Tên sản phẩm", request.ProductName);
        Append(builder, "Mã màu", request.ColourCode);
        Append(builder, "Tên màu", request.ColourName);
        Append(builder, "Phụ gia", request.Additive);
        Append(builder, "Tỷ lệ sử dụng", request.UsageRate);
        Append(builder, "Yêu cầu sản phẩm", request.ProductRequirement);
        Append(builder, "Ghi chú Lab", request.LabComment);

        var message = builder.ToString().Trim();
        return message.Length <= 2000
            ? message
            : string.Concat(message.AsSpan(0, 1997), "...");
    }

    private static void Append(StringBuilder builder, string label, object? value)
    {
        var text = FormatValue(value);
        if (!string.IsNullOrWhiteSpace(text))
        {
            builder.AppendLine($"{label}: {text}");
        }
    }

    private static string? FormatValue(object? value)
    {
        return value switch
        {
            null => null,
            string text => string.IsNullOrWhiteSpace(text) ? null : text.Trim(),
            DateTime date => date.ToString("yyyy-MM-dd"),
            bool flag => flag ? "Có" : "Không",
            _ => value.ToString()
        };
    }

    private async Task<OperationResult<Guid>> ResolveManagerByAsync(
        Guid customerId,
        Guid companyId,
        Guid currentEmployeeId,
        CancellationToken cancellationToken)
    {
        var assignmentEmployeeId = await _dbContext.CustomerAssignments
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                x.CustomerId == customerId &&
                x.CompanyId == companyId)
            .OrderByDescending(x => x.EmployeeId == currentEmployeeId)
            .ThenByDescending(x => x.UpdatedDate)
            .Select(x => x.EmployeeId)
            .FirstOrDefaultAsync(cancellationToken);

        if (assignmentEmployeeId != Guid.Empty)
        {
            return OperationResult<Guid>.Ok(assignmentEmployeeId);
        }

        var now = DateTime.Now;
        var activeClaims = _dbContext.CustomerClaims
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                x.CustomerId == customerId &&
                x.CompanyId == companyId &&
                x.ExpiresAt > now);

        var claimCount = await activeClaims.CountAsync(cancellationToken);
        if (claimCount == 0)
        {
            return OperationResult<Guid>.Fail("Customer has no active assignment or claim.");
        }

        var claimEmployeeId = claimCount > 1
            ? await activeClaims
                .Where(x => x.EmployeeId == currentEmployeeId)
                .Select(x => x.EmployeeId)
                .FirstOrDefaultAsync(cancellationToken)
            : await activeClaims
                .Select(x => x.EmployeeId)
                .FirstOrDefaultAsync(cancellationToken);

        return claimEmployeeId == Guid.Empty
            ? OperationResult<Guid>.Fail("Customer has multiple active claims but none belongs to current user.")
            : OperationResult<Guid>.Ok(claimEmployeeId);
    }

    /// <summary>
    /// Kiểm tra tồn tại của product và tạo product
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private async Task<OperationResult<Guid>> ResolveProductIdAsync(
        CreateSampleRequestCommand request,
        CancellationToken cancellationToken)
    {
        if (request.ProductId is { } productId && productId != Guid.Empty)
        {
            var productExists = await _dbContext.Products
                .AsNoTracking()
                .AnyAsync(x => x.ProductId == productId && x.IsActive, cancellationToken);

            return productExists
                ? OperationResult<Guid>.Ok(productId)
                : OperationResult<Guid>.Fail("Product does not exist or is inactive.");
        }

        if (request.CategoryId is null || request.CategoryId == Guid.Empty)
        {
            return OperationResult<Guid>.Fail("Product CategoryId is invalid.");
        }

        var newProduct = new Product
        {
            ProductId = Guid.CreateVersion7(),
            CompanyId = request.CompanyId,
            IsActive = true
        };

        ApplyProductValues(newProduct, request);

        if (request.ColourCode is not null)
        {
            var colourCodeResult = await SampleRequestColourCodeGenerator.ResolveAsync(
                _dbContext,
                request.ColourCode,
                excludedProductId: newProduct.ProductId,
                currentColourCode: null,
                cancellationToken);

            if (!colourCodeResult.Success)
            {
                return OperationResult<Guid>.Fail(colourCodeResult.Message ?? "ColourCode is invalid.");
            }

            newProduct.ColourCode = colourCodeResult.Data?.ColourCode;

            if (!string.IsNullOrWhiteSpace(colourCodeResult.Data?.AdditiveCode))
            {
                newProduct.Additive = colourCodeResult.Data.AdditiveCode;
            }
        }

        if (!string.IsNullOrWhiteSpace(newProduct.ColourCode))
        {
            var duplicated = await _dbContext.Products
                .AsNoTracking()
                .AnyAsync(x =>
                    x.ProductId != newProduct.ProductId &&
                    x.ColourCode == newProduct.ColourCode &&
                    x.IsActive,
                    cancellationToken);

            if (duplicated)
            {
                return OperationResult<Guid>.Fail("ColourCode already exists.");
            }
        }

        await _dbContext.Products.AddAsync(newProduct, cancellationToken);

        return OperationResult<Guid>.Ok(newProduct.ProductId);
    }


    private static void ApplyProductValues(
        Product product,
        CreateSampleRequestCommand request)
    {
        product.Name = TrimToNull(request.ProductName);
        product.ColourName = TrimToNull(request.ColourName);
        product.Additive = TrimToNull(request.Additive);
        product.UsageRate = request.UsageRate;
        product.DeltaE = TrimToNull(request.DeltaE);
        product.Requirement = TrimToNull(request.ProductRequirement);
        product.ExpiryType = TrimToNull(request.ExpiryType);
        product.StorageCondition = request.StorageCondition;
        product.LabComment = TrimToNull(request.LabComment);
        product.Procedure = TrimToNull(request.Procedure);
        product.RecycleRate = request.RecycleRate;
        product.TaicalRate = request.TaicalRate;
        product.Application = TrimToNull(request.Application);
        product.ProductUsage = TrimToNull(request.ProductUsage);
        product.PolymerMatchedIn = TrimToNull(request.PolymerMatchedIn);
        product.Code = TrimToNull(request.ProductCode);
        product.EndUser = TrimToNull(request.EndUser);
        product.FoodSafety = request.FoodSafety;
        product.RohsStandard = request.RohsStandard;
        product.ReachStandard = request.ReachStandard;
        product.MaxTemp = request.MaxTemp;
        product.WeatherResistance = TrimToNull(request.WeatherResistance);
        product.LightCondition = TrimToNull(request.LightCondition);
        product.VisualTest = TrimToNull(request.VisualTest);
        product.ReturnSample = request.ReturnSample;
        product.IsRecycle = request.IsRecycle ?? false;
        product.OtherComment = TrimToNull(request.ProductOtherComment);
        product.CategoryId = request.CategoryId!.Value;
        product.Weight = request.Weight;
        product.Unit = TrimToNull(request.Unit);
    }
}
