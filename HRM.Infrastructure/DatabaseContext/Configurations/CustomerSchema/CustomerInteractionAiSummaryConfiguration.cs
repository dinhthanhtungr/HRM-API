using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.CustomerSchema;

public sealed class CustomerInteractionAiSummaryConfiguration
    : IEntityTypeConfiguration<CustomerInteractionAiSummary>
{
    public void Configure(EntityTypeBuilder<CustomerInteractionAiSummary> entity)
    {
        entity.HasKey(e => e.Id).HasName("PK_CustomerInteractionAiSummaries_Id");
        entity.ToTable("CustomerInteractionAiSummaries", "Customer");

        entity.Property(e => e.Id)
            .HasColumnName("Id")
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("gen_random_uuid()");

        entity.Property(e => e.CustomerId).HasColumnName("CustomerId").IsRequired();
        entity.Property(e => e.SaleEmployeeId).HasColumnName("SaleEmployeeId");
        entity.Property(e => e.CompanyId).HasColumnName("CompanyId").IsRequired();
        entity.Property(e => e.SummaryScope)
            .HasColumnName("SummaryScope")
            .HasConversion<int>()
            .HasDefaultValue(CustomerInteractionSummaryScope.Monthly);
        entity.Property(e => e.Year).HasColumnName("Year");
        entity.Property(e => e.Month).HasColumnName("Month");
        entity.Property(e => e.PeriodFrom).HasColumnName("PeriodFrom").IsRequired();
        entity.Property(e => e.PeriodTo).HasColumnName("PeriodTo").IsRequired();
        entity.Property(e => e.InteractionCount).HasColumnName("InteractionCount").HasDefaultValue(0);
        entity.Property(e => e.PreviousSummary).HasColumnName("PreviousSummary").HasColumnType("text").HasDefaultValue(string.Empty);
        entity.Property(e => e.Summary).HasColumnName("Summary").HasColumnType("text").HasDefaultValue(string.Empty);
        entity.Property(e => e.CustomerNeed).HasColumnName("CustomerNeed").HasColumnType("text").HasDefaultValue(string.Empty);
        entity.Property(e => e.CurrentStage).HasColumnName("CurrentStage").HasColumnType("text").HasDefaultValue(string.Empty);
        entity.Property(e => e.NextAction).HasColumnName("NextAction").HasColumnType("text").HasDefaultValue(string.Empty);
        entity.Property(e => e.Risk).HasColumnName("Risk").HasColumnType("text").HasDefaultValue(string.Empty);
        entity.Property(e => e.Sentiment).HasColumnName("Sentiment").HasColumnType("citext").HasMaxLength(50).HasDefaultValue(string.Empty);
        entity.Property(e => e.SourceModel).HasColumnName("SourceModel").HasColumnType("citext").HasMaxLength(100).HasDefaultValue(string.Empty);
        entity.Property(e => e.PromptVersion).HasColumnName("PromptVersion").HasColumnType("citext").HasMaxLength(50).HasDefaultValue("v1");
        entity.Property(e => e.IsAiSuccess).HasColumnName("IsAiSuccess").HasDefaultValue(false);
        entity.Property(e => e.IsAiSkipped).HasColumnName("IsAiSkipped").HasDefaultValue(false);
        entity.Property(e => e.AiErrorMessage).HasColumnName("AiErrorMessage").HasColumnType("text");
        entity.Property(e => e.AiGeneratedDate).HasColumnName("AiGeneratedDate");
        entity.Property(e => e.CreatedDate).HasColumnName("CreatedDate");
        entity.Property(e => e.CreatedBy).HasColumnName("CreatedBy");
        entity.Property(e => e.UpdatedDate).HasColumnName("UpdatedDate");
        entity.Property(e => e.UpdatedBy).HasColumnName("UpdatedBy");
        entity.Property(e => e.IsActive).HasColumnName("IsActive").HasDefaultValue(true);

        entity.HasIndex(e => new { e.CompanyId, e.CustomerId, e.SummaryScope, e.PeriodFrom, e.PeriodTo })
            .HasDatabaseName("IX_CustomerInteractionAiSummaries_Customer_Period");
        entity.HasIndex(e => new { e.CompanyId, e.SaleEmployeeId, e.SummaryScope, e.PeriodFrom, e.PeriodTo })
            .HasDatabaseName("IX_CustomerInteractionAiSummaries_Sale_Period");
        entity.HasIndex(e => new { e.CompanyId, e.Year, e.Month, e.SummaryScope })
            .HasDatabaseName("IX_CustomerInteractionAiSummaries_Company_Year_Month");
        entity.HasIndex(e => new { e.IsAiSuccess, e.IsAiSkipped, e.AiGeneratedDate })
            .HasDatabaseName("IX_CustomerInteractionAiSummaries_AiStatus");

        entity.HasOne(e => e.Customer)
            .WithMany(c => c.CustomerInteractionAiSummaries)
            .HasForeignKey(e => e.CustomerId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_CustomerInteractionAiSummaries_Customer");

        entity.HasOne(e => e.Company)
            .WithMany(c => c.CustomerInteractionAiSummaries)
            .HasForeignKey(e => e.CompanyId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_CustomerInteractionAiSummaries_Company");

        entity.HasOne(e => e.SaleEmployee)
            .WithMany(e => e.CustomerInteractionAiSummarySaleEmployees)
            .HasForeignKey(e => e.SaleEmployeeId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("FK_CustomerInteractionAiSummaries_SaleEmployee");

        entity.HasOne(e => e.CreatedByNavigation)
            .WithMany(e => e.CustomerInteractionAiSummaryCreatedByNavigations)
            .HasForeignKey(e => e.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_CustomerInteractionAiSummaries_CreatedBy");

        entity.HasOne(e => e.UpdatedByNavigation)
            .WithMany(e => e.CustomerInteractionAiSummaryUpdatedByNavigations)
            .HasForeignKey(e => e.UpdatedBy)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("FK_CustomerInteractionAiSummaries_UpdatedBy");
    }
}
