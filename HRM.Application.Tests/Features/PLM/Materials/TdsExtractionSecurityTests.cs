using HRM.Application.Abstractions.Security;
using System.Reflection;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.CompanySchema;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.Materials.TdsExtraction;
using HRM.Infrastructure.Services.Geminis.Materials;
using Microsoft.Extensions.Options;

namespace HRM.Application.Tests.Features.PLM.Materials;

public sealed class TdsExtractionSecurityTests
{
    [Theory]
    [InlineData(0, false, false, false)]
    [InlineData(1, true, false, true)]
    [InlineData(1, false, false, false)]
    [InlineData(2, true, false, false)]
    [InlineData(2, true, true, true)]
    [InlineData(2, false, true, false)]
    [InlineData(2, false, false, true, true)]
    [InlineData(2, false, true, false, true)]
    [InlineData(2, false, false, false, true, false)]
    public async Task SourceJobUsesConfiguredOwnerOrSoleCompany(int count, bool ownerCalling,
        bool configured, bool expected, bool shared = false, bool allBatches = true)
    {
        var owner = Guid.NewGuid();
        var other = Guid.NewGuid();
        var user = new User { CompanyId = ownerCalling ? owner : other };
        var db = DispatchProxy.Create<IPLMReadDbContext, ProductionOrderTestStore>();
        var companies = new[] { new Company { CompanyId = owner }, new Company { CompanyId = other } };
        ((ProductionOrderTestStore)(object)db).Seed(nameof(db.Companies), companies.Take(count).ToArray());
        var settings = new TdsExtractionOptions { Enabled = true, SourceCompanyId = configured ? owner : null,
            SharedSourceExport = shared };
        var access = new TdsExtractionAccess(user, new CurrentUserPermissionService(user));
        var result = await access.StartAsync(CreateQueue(settings), db, settings, null, default, allSourceBatches: allBatches);
        Assert.Equal(expected, result.Accepted);
        Assert.Equal(expected ? null : "source_not_authorized", result.ErrorCode);
    }

    [Theory]
    [InlineData(ApplicationRoles.Admin, true)]
    [InlineData(ApplicationRoles.Developer, true)]
    [InlineData(ApplicationRoles.President, true)]
    [InlineData(ApplicationRoles.Sales.SaleUser, false)]
    [InlineData(ApplicationRoles.Accounting.ACCUser, false)]
    public void CapabilitySeparatesExtractionFromGeneralReadAccess(string role, bool expected)
    {
        var user = new User { Roles = [role] };
        var access = new TdsExtractionAccess(user, new CurrentUserPermissionService(user));
        Assert.Equal(expected, access.CanExtract);
    }

    [Fact]
    public void ExplicitEmptyPermissionsUnauthenticatedOrMissingCompanyFailClosed()
    {
        foreach (var user in new[] { new User { HasExplicitPermissionSet = true }, new User { IsAuthenticated = false },
            new User { CompanyId = null }, new User { UserId = Guid.Empty } })
        {
            var permission = new CurrentUserPermissionService(user);
            Assert.False(new TdsExtractionAccess(user, permission).CanExtract);
            Assert.False(permission.HasPermission("unknown.permission"));
        }
        var allowed = new User { HasExplicitPermissionSet = true, Roles = [],
            Permissions = [ApplicationPermissions.PLM.ExtractMaterialDocuments] };
        Assert.True(new TdsExtractionAccess(allowed, new CurrentUserPermissionService(allowed)).CanExtract);
    }

    [Fact]
    public void QueueDoesNotExposeStatusOrExportAcrossCompanyOrOwner()
    {
        var queue = CreateQueue();
        var company = Guid.NewGuid();
        var owner = Guid.NewGuid();
        var started = queue.TryEnqueue(company, owner, [new("tds.txt", [65])]);
        var id = started.Job!.JobId;
        queue.Complete(id, [1, 2, 3]);
        Assert.Null(queue.Get(id, Guid.NewGuid(), owner));
        Assert.Null(queue.GetExcel(id, Guid.NewGuid(), owner));
        Assert.Null(queue.Get(id, company, Guid.NewGuid()));
        Assert.Null(queue.GetExcel(id, company, Guid.NewGuid()));
        Assert.Equal(new byte[] { 1, 2, 3 }, queue.GetExcel(id, company, owner));
    }

    [Fact]
    public void QueueBoundsRetentionAndAllowsOnlyOneActiveJobPerCompany()
    {
        var queue = CreateQueue(new() { Enabled = true, MaxRetainedJobs = 1 });
        var company = Guid.NewGuid();
        var first = queue.TryEnqueue(company, Guid.NewGuid(), [new("tds.txt", [65])]);
        Assert.True(first.Accepted);
        Assert.Equal("company_job_active", queue.TryEnqueue(company, Guid.NewGuid(), [new("tds.txt", [65])]).ErrorCode);
        queue.Complete(first.Job!.JobId, [1]);
        Assert.Equal("job_capacity_reached", queue.TryEnqueue(Guid.NewGuid(), Guid.NewGuid(), [new("tds.txt", [65])]).ErrorCode);
    }

    [Fact]
    public void TemporaryJobRemovalRequiresOwnerAndTerminalStatusAndReleasesCapacity()
    {
        var queue = CreateQueue(new() { Enabled = true, MaxRetainedJobs = 1 });
        var company = Guid.NewGuid();
        var owner = Guid.NewGuid();
        var result = queue.TryEnqueue(company, owner, [new("tds.txt", [65])]);
        Assert.False(queue.Remove(result.Job!.JobId, company, owner));
        queue.Complete(result.Job.JobId, [1]);
        Assert.False(queue.Remove(result.Job.JobId, company, Guid.NewGuid()));
        Assert.True(queue.Remove(result.Job.JobId, company, owner));
        Assert.True(queue.TryEnqueue(company, owner, [new("tds.txt", [65])]).Accepted);
    }

    [Fact]
    public void SourceRequiresExplicitCompanyBindingAndValidBatch()
    {
        var company = Guid.NewGuid();
        var queue = CreateQueue(new() { Enabled = true, SourceCompanyId = company });
        Assert.Equal("source_not_authorized", queue.TryEnqueue(Guid.NewGuid(), Guid.NewGuid(), null).ErrorCode);
        Assert.Equal("invalid_source_batch", queue.TryEnqueue(company, Guid.NewGuid(), null, -1).ErrorCode);
        Assert.True(queue.TryEnqueue(company, Guid.NewGuid(), null, 30, 10).Accepted);
    }

    [Theory]
    [InlineData("../tds.pdf", 1, "invalid_file_name")]
    [InlineData("C:\\tds.pdf", 1, "invalid_file_name")]
    [InlineData("tds.exe", 1, "unsupported_file_type")]
    [InlineData("tds.pdf", 0, "file_size_limit")]
    [InlineData("tds.pdf", 11 * 1024 * 1024, "file_size_limit")]
    public void FileValidationRejectsTraversalUnsupportedAndOversizedFiles(string name, long size, string code)
        => Assert.Equal(code, Assert.Throws<TdsProcessingException>(() => TdsExtractionRules.ValidateFile(name, size, new())).Code);

    [Fact]
    public async Task BoundedReaderStopsEvenWhenStreamHasNoTrustedLength()
    {
        using var stream = new MemoryStream(new byte[100]);
        var error = await Assert.ThrowsAsync<TdsProcessingException>(() => TdsFileReader.ReadBoundedAsync(stream, 99, default));
        Assert.Equal("file_size_limit", error.Code);
    }

    private static TdsJobQueue CreateQueue(TdsExtractionOptions? options = null)
        => new(Options.Create(options ?? new() { Enabled = true }));

    private sealed class User : ICurrentUser
    {
        public bool IsAuthenticated { get; init; } = true;
        public Guid UserId { get; init; } = Guid.NewGuid();
        public Guid? EmployeeId => null;
        public Guid? CompanyId { get; init; } = Guid.NewGuid();
        public string? UserName => null;
        public string? Email => null;
        public IReadOnlyCollection<string> Roles { get; init; } = [ApplicationRoles.Admin];
        public IReadOnlyCollection<string> Permissions { get; init; } = [];
        public bool HasExplicitPermissionSet { get; init; }
        public bool IsInRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }
}
