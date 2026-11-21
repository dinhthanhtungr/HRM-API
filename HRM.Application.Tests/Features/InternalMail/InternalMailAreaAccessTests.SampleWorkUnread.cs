using HRM.Application.Features.InternalMail.Services;
using HRM.Domain.Enums.InternalMailEnums;
namespace HRM.Application.Tests.Features.InternalMail;
public sealed partial class InternalMailAreaAccessTests
{
    [Fact]
    public async Task SampleWorkUnreadRejectsUnboundedBatchesAndEmptyScope()
    {
        var user = new User("LabUser");
        var store = CreateStore(user, InternalMailRelatedType.Internal, true);
        var access = new InternalMailAreaAccessService(store.Db, user);
        Assert.Empty(await access.SampleWorkUnreadAsync([], default));
        await Assert.ThrowsAsync<ArgumentException>(() => access.SampleWorkUnreadAsync(new Guid[101], default));
        user.CompanyId = null;
        Assert.Empty(await access.SampleWorkUnreadAsync([Guid.NewGuid()], default));
    }
}
