using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Entities.OrderSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Abstractions.Persistence.Executive;

/// <summary>
/// Read surface for executive projections that need PLM, pricing and Internal Mail
/// in one database-translated query.
/// </summary>
public interface IExecutiveReadDbContext
{
    DbSet<SampleRequest> SampleRequests { get; }
    DbSet<Customer> Customers { get; }
    DbSet<QuotationLine> QuotationLines { get; }
    DbSet<ProductPricingVersion> ProductPricingVersions { get; }
    DbSet<Formula> Formulas { get; }
    DbSet<Product> Products { get; }
    DbSet<MerchandiseOrder> MerchandiseOrders { get; }
    DbSet<MerchandiseOrderDetail> MerchandiseOrderDetails { get; }
    DbSet<InternalConversation> InternalConversations { get; }
    DbSet<InternalConversationParticipant> InternalConversationParticipants { get; }
}
