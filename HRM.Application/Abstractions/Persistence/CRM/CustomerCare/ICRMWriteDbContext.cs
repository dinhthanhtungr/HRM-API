using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.AuditSchema;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Entities.WorkTaskSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Abstractions.Persistence.CRM.CustomerCare;

public interface ICRMWriteDbContext
{
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<Address> Addresses { get; }
    DbSet<Contact> Contacts { get; }
    DbSet<Customer> Customers { get; }
    DbSet<CustomerClaim> CustomerClaims { get; }
    DbSet<CustomerNote> CustomerNotes { get; }
    DbSet<CustomerAssignment> CustomerAssignments { get; }
    DbSet<CustomerTransferLog> CustomerTransferLogs { get; }
    DbSet<DetailCustomerTransfer> DetailCustomerTransfers { get; }
    DbSet<CustomerInteraction> CustomerInteractions { get; }
    DbSet<CustomerInteractionReference> CustomerInteractionReferences { get; }
    DbSet<CustomerInteractionAiSummary> CustomerInteractionAiSummaries { get; }
    DbSet<Quotation> Quotations { get; }
    DbSet<QuotationLine> QuotationLines { get; }
    DbSet<QuotationLinePriceTier> QuotationLinePriceTiers { get; }
    DbSet<QuotationTerm> QuotationTerms { get; }
    DbSet<QuotationStatusHistory> QuotationStatusHistories { get; }
    DbSet<ProductPricingVersion> ProductPricingVersions { get; }
    DbSet<ProductPricingTier> ProductPricingTiers { get; }
    DbSet<FormulaPricingPolicy> FormulaPricingPolicies { get; }
    DbSet<FormulaPricingPolicyTier> FormulaPricingPolicyTiers { get; }
    DbSet<Category> Categories { get; }
    DbSet<Formula> Formulas { get; }
    DbSet<SampleRequest> SampleRequests { get; }
    DbSet<SampleRequestSampleTrial> SampleRequestSampleTrials { get; }

    void ClearTrackedChanges();

    // Legacy mappings are retained only so existing database/model code keeps compiling.
    DbSet<CustomerFollowUpTask> CustomerFollowUpTasks { get; }
    DbSet<CustomerWorkPlan> CustomerWorkPlans { get; }

    DbSet<WorkTask> WorkTasks { get; }
    DbSet<WorkTaskList> WorkTaskLists { get; }
    DbSet<WorkTaskAssignee> WorkTaskAssignees { get; }
    DbSet<WorkTaskReference> WorkTaskReferences { get; }
    DbSet<WorkPlan> WorkPlans { get; }
    DbSet<WorkPlanAssignee> WorkPlanAssignees { get; }
    DbSet<WorkPlanReference> WorkPlanReferences { get; }
    DbSet<Employee> Employees { get; }
    DbSet<Group> Groups { get; }
    DbSet<MemberInGroup> MemberInGroups { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
