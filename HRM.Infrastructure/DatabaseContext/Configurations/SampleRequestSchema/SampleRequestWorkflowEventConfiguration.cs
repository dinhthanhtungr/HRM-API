using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.SampleRequestSchema;

public sealed class SampleRequestWorkflowEventConfiguration : IEntityTypeConfiguration<SampleRequestWorkflowEvent>
{
    public void Configure(EntityTypeBuilder<SampleRequestWorkflowEvent> entity)
    {
        entity.ToTable("SampleRequestWorkflowEvents", "SampleRequests");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).ValueGeneratedNever();
        entity.Property(x => x.EventType).HasMaxLength(80).IsRequired();
        entity.Property(x => x.OccurredAt).HasColumnType("timestamp without time zone");
        entity.Property(x => x.RecordedAt).HasColumnType("timestamp without time zone");
        entity.Property(x => x.Note).HasColumnType("text");
        entity.Property(x => x.PriceReference).HasMaxLength(500);
        entity.HasIndex(x => new { x.CompanyId, x.SampleRequestId, x.RecordedAt, x.Id })
            .HasDatabaseName("IX_SampleRequestWorkflowEvents_Request_Recorded");
        entity.HasIndex(x => new { x.CompanyId, x.SampleRequestId, x.EventType, x.RecordedAt, x.Id })
            .HasDatabaseName("IX_SampleRequestWorkflowEvents_Request_Type_Recorded");
        entity.HasIndex(x => x.SampleRequestSampleTrialId);
        entity.HasIndex(x => x.RelatedEventId);
        entity.HasIndex(x => x.MessageId);
        entity.HasIndex(x => x.NotificationId);
        entity.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_WorkflowEvents_Company");
        entity.HasOne<SampleRequest>().WithMany().HasForeignKey(x => x.SampleRequestId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_WorkflowEvents_Request");
        entity.HasOne<Employee>().WithMany().HasForeignKey(x => x.RecordedByEmployeeId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_WorkflowEvents_RecordedBy");
        entity.HasOne<Formula>().WithMany().HasForeignKey(x => x.FormulaId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_WorkflowEvents_Formula");
        entity.HasOne<SampleRequestSampleTrial>().WithMany().HasForeignKey(x => x.SampleRequestSampleTrialId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_WorkflowEvents_Trial");
        entity.HasOne<ProductPricingVersion>().WithMany().HasForeignKey(x => x.ProductPricingVersionId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_WorkflowEvents_Price");
        entity.HasOne<QuotationLine>().WithMany().HasForeignKey(x => x.QuotationLineId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_WorkflowEvents_QuotationLine");
        entity.HasOne<SampleRequestWorkflowEvent>().WithMany().HasForeignKey(x => x.RelatedEventId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_WorkflowEvents_RelatedEvent");
    }
}
