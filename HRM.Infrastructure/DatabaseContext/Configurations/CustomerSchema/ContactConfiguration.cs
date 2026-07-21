using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.CustomerSchema;

namespace HRM.Infrastructure.DatabaseContext.Configurations.CustomerSchema
{
    public class ContactConfiguration : IEntityTypeConfiguration<Contact>
    {
        public void Configure(EntityTypeBuilder<Contact> entity)
        {
            entity.HasKey(e => e.ContactId).HasName("PK__Contacts__5C6625BB570D4F62");
            entity.ToTable("Contacts", "Customer");

            entity.Property(e => e.ContactId)
                  .HasColumnName("ContactId")
                  .ValueGeneratedOnAdd()
                  .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.CustomerId).HasColumnName("CustomerId");
            entity.Property(e => e.Email).HasColumnName("Email").HasColumnType("citext");
            entity.Property(e => e.FirstName).HasColumnName("FirstName").HasColumnType("citext");
            entity.Property(e => e.Gender).HasColumnName("Gender").HasColumnType("citext");
            entity.Property(e => e.LastName).HasColumnName("LastName").HasColumnType("citext");
            entity.Property(e => e.Phone).HasColumnName("Phone").HasColumnType("citext");

            entity.HasIndex(e => e.CustomerId).HasDatabaseName("IX_Contacts_CustomerId");

            // XÓA CUSTOMER -> XÓA CONTACT
            entity.HasOne(d => d.Customer)
                  .WithMany(p => p.Contacts)
                  .HasForeignKey(d => d.CustomerId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("FK_Contacts_Customer");
        }
    }
}
