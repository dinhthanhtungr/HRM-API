using System.Collections.Concurrent;
using System.Reflection;
using HRM.Application.Abstractions.FileStorage;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestGallery;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestGalleryImages;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestGalleryImageContent;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.AttachmentSchema;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.Attachment;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleRequestGalleryTests
{
    [Fact]
    public void GalleryRejectsOffsetOverflowAndKeepsSummaryFilterValidation()
    {
        foreach (var query in new[] {
            new GetSampleRequestGalleryQuery { PageNumber = int.MaxValue, PageSize = 100 },
            new GetSampleRequestGalleryQuery { CustomerIds = [Guid.Empty] }
        })
        {
            var errors = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
            Assert.False(System.ComponentModel.DataAnnotations.Validator.TryValidateObject(query,
                new System.ComponentModel.DataAnnotations.ValidationContext(query), errors, true));
        }
    }
    [Fact]
    public void PageProjectionTranslatesToPostgresWithoutFormulaOrOriginalFilePayload()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var sql = GetSampleRequestGalleryQueryHandler.ProjectPage(db,
            db.SampleRequests.Where(x => x.CompanyId == Guid.Empty),
            new GetSampleRequestGalleryQuery { PageSize = 10, PageNumber = 2, SortBy = "createdDate", SortDirection = "desc" })
            .ToQueryString();
        Assert.Contains("LIMIT", sql);
        Assert.Contains("OFFSET", sql);
        Assert.Contains("AttachmentCollectionId", sql);
        Assert.Contains("ColourName", sql);
        Assert.Contains("CategoryName", sql);
        Assert.Contains("Additive", sql);
        Assert.DoesNotContain("Formula", sql);
        Assert.DoesNotContain("StoragePath", sql);
        Assert.DoesNotContain("ContentHash", sql);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GalleryKeepsScopePaginationAndRequestsWithoutImages(bool fullView)
    {
        var f = new Fixture(fullView);
        f.Request.Product.ColourName = "White";
        f.Request.Product.Additive = "LEGACY_ADDITIVE";
        f.Request.Product.Category = new() { Name = "Masterbatch" };
        var otherCustomer = new Customer { CustomerId = Guid.NewGuid(), CompanyId = f.Company, IsActive = true };
        f.Store.Seed(nameof(f.Db.Customers), f.Customer, otherCustomer);
        var requests = new[] { f.Request, f.Row(f.Company, otherCustomer.CustomerId), f.Row(Guid.NewGuid(), f.Customer.CustomerId), f.Row(f.Company, f.Customer.CustomerId) };
        requests[3].IsActive = false;
        f.Store.Seed(nameof(f.Db.SampleRequests), requests);
        f.Store.Seed(nameof(f.Db.AttachmentModels), f.Image,
            f.ImageCopy("report.pdf"), f.ImageCopy("private.png", AttachmentSlot.InternalMail),
            f.ImageCopy("inactive.png", active: false));
        var handler = new GetSampleRequestGalleryQueryHandler(f.Db, f.Visibility);
        var result = await handler.Handle(new GetSampleRequestGalleryQuery { SortBy = "createdDate", SortDirection = "desc", PageSize = 1 }, default);
        Assert.Equal(fullView ? 2 : 1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal(1, result.PageSize);
        if (fullView)
        {
            // Later request has no images and remains part of the list.
            Assert.Equal(0, result.Items[0].ImageCount);
            Assert.Null(result.Items[0].CoverImage);
        }
        else
        {
            var item = result.Items[0];
            Assert.Equal(f.Request.SampleRequestId, item.SampleRequestId);
            Assert.Equal(1, item.ImageCount);
            Assert.Equal("White", item.ColorName);
            Assert.Equal("Masterbatch", item.CategoryName);
            Assert.Equal("LEGACY_ADDITIVE", item.AdditiveName);
            Assert.Contains($"/{f.Request.SampleRequestId}/attachments/{f.Image.AttachmentId}/thumbnail", item.CoverImage!.ThumbnailUrl);
        }
    }

    [Theory]
    [InlineData("otherCompany")]
    [InlineData("otherOwner")]
    [InlineData("inactiveRequest")]
    [InlineData("otherCollection")]
    [InlineData("inactiveImage")]
    [InlineData("privateSlot")]
    public async Task ImageAccessFailsBeforeReadingEvenAnExistingThumbnail(string scenario)
    {
        var f = new Fixture();
        if (scenario == "otherCompany") f.Request.CompanyId = Guid.NewGuid();
        if (scenario == "otherOwner") f.Request.CustomerId = Guid.NewGuid();
        if (scenario == "inactiveRequest") f.Request.IsActive = false;
        if (scenario == "otherCollection") f.Image.AttachmentCollectionId = Guid.NewGuid();
        if (scenario == "inactiveImage") f.Image.IsActive = false;
        if (scenario == "privateSlot") f.Image.Slot = AttachmentSlot.InternalMail;
        var storage = new MemoryStorage();
        storage.Files[GalleryThumbnailStorage.PathFor(f.Image)] = [1, 2, 3];
        var handler = new GetSampleRequestGalleryImageContentQueryHandler(f.Db, f.Visibility, storage, new Generator());
        foreach (var thumbnail in new[] { false, true })
        {
            Assert.Null(await handler.Handle(new(f.Request.SampleRequestId, f.Image.AttachmentId, thumbnail), default));
        }
        Assert.Equal(0, storage.Reads);
    }

    [Fact]
    public async Task ImageListChecksParentAndOnlyListsSampleImages()
    {
        var f = new Fixture();
        f.Store.Seed(nameof(f.Db.AttachmentModels), f.Image, f.ImageCopy("secret.jpg", AttachmentSlot.InternalMail));
        var handler = new GetSampleRequestGalleryImagesQueryHandler(f.Db, f.Visibility);
        var images = await handler.Handle(new(f.Request.SampleRequestId), default);
        Assert.Single(images!);
        Assert.Contains("gallery-content", images![0].Url);
        f.Request.CompanyId = Guid.NewGuid();
        Assert.Null(await handler.Handle(new(f.Request.SampleRequestId), default));
    }

    [Fact]
    public async Task ConcurrentThumbnailRequestsGenerateOnceAndReuseOnlyAfterPermissionCheck()
    {
        var f = new Fixture();
        var storage = new MemoryStorage();
        storage.Files[f.Image.StoragePath] = [1, 2, 3];
        var generator = new Generator();
        var handler = new GetSampleRequestGalleryImageContentQueryHandler(f.Db, f.Visibility, storage, generator);
        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
            handler.Handle(new(f.Request.SampleRequestId, f.Image.AttachmentId, true), default)));
        Assert.Equal(1, generator.Calls);
        Assert.Equal(1, storage.Writes);
        foreach (var content in results)
        {
            Assert.Equal("image/webp", content!.ContentType);
            await content.Stream.DisposeAsync();
        }
        f.Request.CompanyId = Guid.NewGuid();
        var reads = storage.Reads;
        Assert.Null(await handler.Handle(new(f.Request.SampleRequestId, f.Image.AttachmentId, true), default));
        Assert.Equal(reads, storage.Reads);
    }

    [Fact]
    public async Task UnsupportedAndMissingFilesDoNotFallBackToOriginalOrWriteThumbnail()
    {
        var f = new Fixture();
        var storage = new MemoryStorage();
        var generator = new Generator { Supported = false };
        var handler = new GetSampleRequestGalleryImageContentQueryHandler(f.Db, f.Visibility, storage, generator);
        Assert.Null(await handler.Handle(new(f.Request.SampleRequestId, f.Image.AttachmentId, true), default));
        storage.Files[f.Image.StoragePath] = [1, 2, 3];
        Assert.Null(await handler.Handle(new(f.Request.SampleRequestId, f.Image.AttachmentId, true), default));
        Assert.Equal(0, storage.Writes);
        f.Image.SizeBytes = GalleryThumbnailStorage.MaxSourceBytes + 1;
        Assert.Null(await handler.Handle(new(f.Request.SampleRequestId, f.Image.AttachmentId, true), default));
        Assert.Equal(1, generator.Calls);
    }

    [Fact]
    public async Task FailedWriteDoesNotPoisonTheThumbnailCache()
    {
        var f = new Fixture();
        var storage = new MemoryStorage { FailWrite = true };
        storage.Files[f.Image.StoragePath] = [1, 2, 3];
        var generator = new Generator();
        var handler = new GetSampleRequestGalleryImageContentQueryHandler(f.Db, f.Visibility, storage, generator);
        await Assert.ThrowsAsync<IOException>(() => handler.Handle(new(f.Request.SampleRequestId, f.Image.AttachmentId, true), default));
        Assert.False(storage.Files.ContainsKey(GalleryThumbnailStorage.PathFor(f.Image)));
        storage.FailWrite = false;
        var result = await handler.Handle(new(f.Request.SampleRequestId, f.Image.AttachmentId, true), default);
        Assert.NotNull(result);
        await result!.Stream.DisposeAsync();
        Assert.Equal(2, generator.Calls);
    }

    [Fact]
    public async Task LegacyMissingSizeMetadataUsesActualStreamSizeWithoutBypassingTheLimit()
    {
        var f = new Fixture();
        f.Image.SizeBytes = 0;
        var storage = new MemoryStorage();
        storage.Files[f.Image.StoragePath] = [1, 2, 3];
        var generator = new Generator();
        var handler = new GetSampleRequestGalleryImageContentQueryHandler(f.Db, f.Visibility, storage, generator);
        var result = await handler.Handle(new(f.Request.SampleRequestId, f.Image.AttachmentId, true), default);
        Assert.NotNull(result);
        await result!.Stream.DisposeAsync();
        Assert.Equal(1, generator.Calls);
        storage.Files.TryRemove(GalleryThumbnailStorage.PathFor(f.Image), out _);
        storage.LengthOverride = GalleryThumbnailStorage.MaxSourceBytes + 1;
        Assert.Null(await handler.Handle(new(f.Request.SampleRequestId, f.Image.AttachmentId, true), default));
        Assert.Equal(1, generator.Calls);
    }

    [Fact]
    public async Task RealThumbnailGeneratorProducesWebpWithinBoundsAndRejectsBrokenImages()
    {
        var type = typeof(ApplicationDbContext).Assembly.GetType("HRM.Infrastructure.Services.FileStorage.ImageSharpThumbnailGenerator")!;
        var generator = (IImageThumbnailGenerator)Activator.CreateInstance(type)!;
        using var source = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgb24>(1200, 800);
        await using var input = new MemoryStream();
        await SixLabors.ImageSharp.ImageExtensions.SaveAsPngAsync(source, input);
        input.Position = 0;
        await using var output = await generator.GenerateWebpAsync(input, 640, 640);
        Assert.NotNull(output);
        var info = await SixLabors.ImageSharp.Image.IdentifyAsync(output!);
        Assert.Equal(640, info.Width);
        Assert.True(info.Height <= 640);
        Assert.Equal("image/webp", info.Metadata.DecodedImageFormat!.DefaultMimeType);
        Assert.Null(await generator.GenerateWebpAsync(new MemoryStream([0, 1, 2]), 640, 640));
    }

    [Fact]
    public async Task ThumbnailGeneratorDecodesOnlyTheFirstAnimationFrame()
    {
        var type = typeof(ApplicationDbContext).Assembly.GetType("HRM.Infrastructure.Services.FileStorage.ImageSharpThumbnailGenerator")!;
        var generator = (IImageThumbnailGenerator)Activator.CreateInstance(type)!;
        using var image = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgb24>(32, 32);
        image.Frames.AddFrame(image.Frames.RootFrame);
        await using var input = new MemoryStream();
        await SixLabors.ImageSharp.ImageExtensions.SaveAsGifAsync(image, input);
        input.Position = 0;
        await using var output = await generator.GenerateWebpAsync(input, 640, 640);
        Assert.NotNull(output);
        using var decoded = await SixLabors.ImageSharp.Image.LoadAsync(output!);
        Assert.Single(decoded.Frames);
        Assert.Equal(32, decoded.Width);
        Assert.Equal(32, decoded.Height);
    }

    public class VisibilityProxy : DispatchProxy
    {
        public ViewerScope Scope { get; set; } = null!;
        private readonly CustomerVisibilityService _real = new(null!, null!, null!);
        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            method!.Name is "BuildScopeAsync" or "BuildReadScopeAsync"
                ? Task.FromResult(Scope) : method.Invoke(_real, args);
    }

    private sealed class Fixture
    {
        public Guid Company { get; } = Guid.NewGuid();
        public IPLMReadDbContext Db { get; }
        public ProductionOrderTestStore Store { get; }
        public ICustomerVisibilityService Visibility { get; }
        public Customer Customer { get; }
        public SampleRequest Request { get; }
        public AttachmentModel Image { get; }
        private int _number;
        public Fixture(bool fullView = false)
        {
            (Db, Store) = ProductionOrderTestStore.CreateContext<IPLMReadDbContext>();
            var employee = Guid.NewGuid();
            Customer = new Customer { CustomerId = Guid.NewGuid(), CompanyId = Company, IsActive = true,
                CustomerAssignments = [new CustomerAssignment { EmployeeId = employee, IsActive = true }] };
            Visibility = DispatchProxy.Create<ICustomerVisibilityService, VisibilityProxy>();
            ((VisibilityProxy)Visibility).Scope = new(Company, employee, fullView, false, new HashSet<Guid>(), new HashSet<Guid> { employee }, DateTime.Now);
            Request = Row(Company, Customer.CustomerId);
            Image = ImageCopy("sample.png");
            Store.Seed(nameof(Db.Customers), Customer);
            Store.Seed(nameof(Db.SampleRequests), Request);
            Store.Seed(nameof(Db.AttachmentModels), Image);
        }
        public SampleRequest Row(Guid company, Guid customer) => new()
        {
            SampleRequestId = Guid.NewGuid(), CompanyId = company, CustomerId = customer,
            AttachmentCollectionId = Guid.NewGuid(), IsActive = true, CreatedDate = new DateTime(2026, 11, 1).AddDays(_number++),
            Product = new Product { Name = "Sample", ColourCode = "BB" }, Customer = Customer
        };
        public AttachmentModel ImageCopy(string name, AttachmentSlot slot = AttachmentSlot.SampleRequest, bool active = true) => new()
        {
            AttachmentId = Guid.NewGuid(), AttachmentCollectionId = Request.AttachmentCollectionId,
            FileName = name, Slot = slot, IsActive = active, SizeBytes = 3,
            StoragePath = "collections/test/sample.png", CreateDate = new DateTime(2026, 11, 1)
        };
    }

    private sealed class Generator : IImageThumbnailGenerator
    {
        public int Calls;
        public bool Supported = true;
        public async Task<Stream?> GenerateWebpAsync(Stream source, int maxWidth, int maxHeight, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref Calls);
            await Task.Yield();
            return Supported ? new MemoryStream([4, 5]) : null;
        }
    }

    private sealed class MemoryStorage : IFileStorage
    {
        public ConcurrentDictionary<string, byte[]> Files { get; } = new();
        public int Reads;
        public int Writes;
        public bool FailWrite;
        public long? LengthOverride;
        public Task<StoredFile> OpenReadAsync(string path, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref Reads);
            if (!Files.TryGetValue(path, out var data)) throw new FileNotFoundException();
            return Task.FromResult(new StoredFile(new MemoryStream(data), "image/png", "sample.png", LengthOverride ?? data.Length));
        }
        public async Task SaveAtPathAsync(Stream stream, string relativePath, CancellationToken cancellationToken = default)
        {
            using var output = new MemoryStream();
            await stream.CopyToAsync(output, cancellationToken);
            Files[relativePath] = output.ToArray();
            Interlocked.Increment(ref Writes);
            if (FailWrite) throw new IOException("Simulated interrupted write.");
        }
        public Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
        {
            Files.TryRemove(relativePath, out _);
            return Task.CompletedTask;
        }
        public Task<string> SaveAsync(Stream stream, string contentType, string fileName, string relativeFolder, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
