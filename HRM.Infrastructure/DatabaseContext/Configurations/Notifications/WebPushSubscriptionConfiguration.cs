using HRM.Domain.Entities.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs.Configurations.Notifications;

/// <summary>
/// Mapping subscription theo tung employee/thiet bi. Endpoint unique giup subscribe lai cung browser tro thanh upsert.
/// </summary>
public sealed class WebPushSubscriptionConfiguration : IEntityTypeConfiguration<WebPushSubscription>
{
    private const string Schema = "notification";

    public void Configure(EntityTypeBuilder<WebPushSubscription> entity)
    {
        entity.ToTable("web_push_subscriptions", Schema);

        entity.HasKey(x => x.WebPushSubscriptionId)
            .HasName("pk_web_push_subscriptions");

        entity.Property(x => x.WebPushSubscriptionId)
            .HasColumnName("web_push_subscription_id")
            .HasDefaultValueSql("gen_random_uuid()");

        entity.Property(x => x.CompanyId)
            .HasColumnName("company_id")
            .IsRequired();

        entity.Property(x => x.EmployeeId)
            .HasColumnName("employee_id")
            .IsRequired();

        entity.Property(x => x.Endpoint)
            .HasColumnName("endpoint")
            .HasMaxLength(2048)
            .IsRequired();

        entity.Property(x => x.P256dh)
            .HasColumnName("p256dh")
            .HasMaxLength(512)
            .IsRequired();

        entity.Property(x => x.Auth)
            .HasColumnName("auth")
            .HasMaxLength(256)
            .IsRequired();

        entity.Property(x => x.DeviceName)
            .HasColumnName("device_name")
            .HasMaxLength(128);

        entity.Property(x => x.UserAgent)
            .HasColumnName("user_agent")
            .HasMaxLength(1024);

        entity.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .ValueGeneratedNever()
            .IsRequired();

        entity.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasPrecision(6)
            .IsRequired();

        entity.Property(x => x.LastSuccessAt)
            .HasColumnName("last_success_at")
            .HasPrecision(6);

        entity.Property(x => x.LastFailureAt)
            .HasColumnName("last_failure_at")
            .HasPrecision(6);

        entity.Property(x => x.FailureCount)
            .HasColumnName("failure_count")
            .ValueGeneratedNever()
            .IsRequired();

        entity.HasIndex(x => x.Endpoint)
            .IsUnique()
            .HasDatabaseName("ux_web_push_subscriptions_endpoint");

        entity.HasIndex(x => new { x.CompanyId, x.EmployeeId, x.IsActive })
            .HasDatabaseName("ix_web_push_subscriptions_company_employee_active");

        entity.HasOne(x => x.Company)
            .WithMany(x => x.WebPushSubscriptions)
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_web_push_subscriptions_company");

        entity.HasOne(x => x.Employee)
            .WithMany(x => x.WebPushSubscriptions)
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_web_push_subscriptions_employee");
    }
}
