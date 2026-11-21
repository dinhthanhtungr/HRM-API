using HRM.Application.Features.CRM.CustomerCare.SampleSharingGroups;
using Microsoft.Extensions.Options;

namespace HRM.Application.Tests.Features.CRM.CustomerCare;

internal static class CustomerSampleSharingTestServices
{
    public static CustomerSampleSharingService Disabled()
        => new(null!, null!, Options.Create(new CustomerSampleSharingOptions()));
}
