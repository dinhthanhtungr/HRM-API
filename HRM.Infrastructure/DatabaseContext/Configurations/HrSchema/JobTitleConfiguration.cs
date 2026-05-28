using HRM.Domain.Entities.HrSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.HrSchema
{
    public class JobTitleConfiguration : IEntityTypeConfiguration<JobTitle>
    {
        public void Configure(EntityTypeBuilder<JobTitle> entity)
        {
            entity.ToTable("job_titles", "hr");

            entity.HasKey(e => e.JobTitleId).HasName("pk_job_titles");

            entity.Property(e => e.JobTitleId)
                  .HasColumnName("job_title_id")
                  .ValueGeneratedNever();

            entity.Property(e => e.Code).HasColumnName("code").HasMaxLength(64);
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
            entity.Property(e => e.EnglishName).HasColumnName("english_name").HasMaxLength(255);
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);

            entity.HasIndex(e => e.Code)
                  .IsUnique()
                  .HasDatabaseName("ux_job_titles_code");
        }
    }
}
