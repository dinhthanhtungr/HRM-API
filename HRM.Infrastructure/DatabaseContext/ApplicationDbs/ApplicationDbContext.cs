
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
//using QuestPDF.Infrastructure;
using HRM.Domain.Entities;
using HRM.Domain.Entities.AttachmentSchema;
using HRM.Domain.Entities.AuditSchema;
using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.DeliverySchema;
using HRM.Domain.Entities.DevandqaSchema;
//using HRM.Domain.Entities.EnergyScheme;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Entities.MROSchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Entities.SupplyRequestSchema;
using HRM.Domain.Entities.WarehouseSchema;




//using System.Text.RegularExpressions;
using HRM.Domain.Identity;
using HRM.Infrastructure.Helpers.IdCounter;


namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs
{
    public partial class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid,
        IdentityUserClaim<Guid>, ApplicationUserRole, IdentityUserLogin<Guid>,
        IdentityRoleClaim<Guid>, IdentityUserToken<Guid>>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {

        }

        public DbSet<IdCounter> IdCounters { get; set; } = default!;

        public virtual DbSet<AttachmentCollection> AttachmentCollections { get; set; }
        public virtual DbSet<AttachmentModel> AttachmentModels { get; set; }


        public virtual DbSet<Formula> Formulas { get; set; }

        public virtual DbSet<FormulaMaterial> FormulaMaterials { get; set; }
        public virtual DbSet<FormulaVersion> FormulaVersions { get; set; }
        public virtual DbSet<FormulaVersionItem> FormulaVersionItems { get; set; }
        public virtual DbSet<ManufacturingVUFormula> ManufacturingVUFormulas { get; set; }
        public virtual DbSet<FormulaMaterialSnapshot> FormulaMaterialSnapshots { get; set; }

        public virtual DbSet<ColorChipRecord> ColorChipRecords { get; set; }
        public virtual DbSet<ColorChipRecordDevelopmentFormula> ColorChipRecordDevelopmentFormulas { get; set; }
        public virtual DbSet<MfgProductionOrdersPlan> MfgProductionOrdersPlans { get; set; }

        public virtual DbSet<Product> Products { get; set; }
        public virtual DbSet<SampleRequest> SampleRequests { get; set; }
        public virtual DbSet<SampleRequestSampleTrial> SampleRequestSampleTrials { get; set; }



        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            AppContext.SetSwitch("Npgsql.EnableParameterLogging", false);
        }
        protected override void ConfigureConventions(ModelConfigurationBuilder builder)
        {
            builder.Properties<DateTime>().HaveColumnType("timestamp without time zone");
            builder.Properties<DateTime?>().HaveColumnType("timestamp without time zone");
        }
        //49
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);


            // Đếm số
            modelBuilder.Entity<IdCounter>(e =>
            {
                e.ToTable("IdCounters", "public");
                e.HasKey(x => new { x.CompanyId, x.Prefix, x.Period });
                //e.HasKey(x => new { x.CompanyId, x.Prefix });
            });


            // Thiết lập khóa chính
            modelBuilder.Entity<ApplicationUserRole>()
                .HasKey(ur => new { ur.UserId, ur.RoleId });

            // Thiết lập quan hệ với ApplicationUser
            modelBuilder.Entity<ApplicationUserRole>()
                .HasOne(ur => ur.User)
                .WithMany(u => u.UserRoles)
                .HasForeignKey(ur => ur.UserId);

            // Thiết lập quan hệ với ApplicationRole
            modelBuilder.Entity<ApplicationUserRole>()
                .HasOne(ur => ur.Role)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(ur => ur.RoleId);


            modelBuilder.Entity<ApplicationUser>()
                .HasOne(u => u.Employee)
                .WithMany(e => e.ApplicationUsers)   // <— CHỈ RÕ NAVIGATION PHÍA Employee
                .HasForeignKey(u => u.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ApplicationUser>()
                .HasIndex(u => u.EmployeeId)
                .IsUnique()
                .HasFilter("\"EmployeeId\" IS NOT NULL")
                .HasDatabaseName("UX_AspNetUsers_EmployeeId_NotNull");

            // ApplicationUser.IsActive is temporarily disabled until AspNetUsers is updated.

            modelBuilder.Entity<AttachmentModel>(entity =>
            {
                entity.HasKey(e => e.AttachmentId).HasName("PK__AttachmentModel__B3C4DAF6D3F2E2B3");
                entity.ToTable("AttachmentModel", "Attachment");

                entity.Property(e => e.AttachmentId)
                    .HasColumnName("AttachmentModelID")
                    .ValueGeneratedOnAdd()
                    .HasValueGenerator<GuidV7Generator>();

                entity.HasOne(d => d.CreatedByNavigation).WithMany()
                    .HasForeignKey(d => d.CreateBy)
                    .HasConstraintName("FK_AttachmentModel_CreatedBy");
            });

            modelBuilder.Entity<AttachmentCollection>(entity =>
            {
                entity.HasKey(e => e.AttachmentCollectionId).HasName("PK__AttachmentCollection__B3C4DAF6D3F2E2B3");
                entity.ToTable("AttachmentCollection", "Attachment");

                entity.Property(e => e.AttachmentCollectionId)
                    .HasColumnName("AttachmentCollectionID")
                    .ValueGeneratedOnAdd()
                    .HasValueGenerator<GuidV7Generator>();
            });



            modelBuilder.Entity<Formula>(entity =>
            {
                entity.ToTable("Formulas", "SampleRequests");
                entity.HasKey(e => e.FormulaId).HasName("PK__Formulas__227429A55C6F1195");

                // ===== Columns (đặt HasColumnName y như property) =====
                entity.Property(e => e.FormulaId)
                    .HasColumnName("FormulaId")
                    .ValueGeneratedOnAdd()
                    .HasDefaultValueSql("gen_random_uuid()");

                entity.Property(e => e.ExternalId).HasColumnName("ExternalId").HasMaxLength(50).IsRequired();
                entity.Property(e => e.Name).HasColumnName("Name").HasMaxLength(200).IsRequired();

                entity.Property(e => e.ProductId).HasColumnName("ProductId");
                entity.Property(e => e.Status).HasColumnName("Status").HasMaxLength(32).HasDefaultValue("Draft");

                entity.Property(e => e.CheckBy).HasColumnName("CheckBy");
                entity.Property(e => e.CheckDate).HasColumnName("CheckDate");

                entity.Property(e => e.SentBy).HasColumnName("SentBy");
                entity.Property(e => e.SentDate).HasColumnName("SentDate");

                entity.Property(e => e.TotalPrice).HasColumnName("TotalPrice").HasPrecision(16, 2);

                entity.Property(e => e.EffectiveDate).HasColumnName("EffectiveDate");
                entity.Property(e => e.ProductionPrice).HasColumnName("ProductionPrice").HasPrecision(16, 2);
                entity.Property(e => e.PresidentPrice).HasColumnName("PresidentPrice").HasPrecision(16, 2);
                entity.Property(e => e.ProfitMarginPrice).HasColumnName("ProfitMarginPrice").HasPrecision(16, 2);

                entity.Property(e => e.IsSelect).HasColumnName("IsSelect").HasDefaultValue(false);
                entity.Property(e => e.IsActive).HasColumnName("IsActive").HasDefaultValue(true);

                entity.Property(e => e.Note).HasColumnName("Note").HasColumnType("text");

                entity.Property(e => e.CreatedBy).HasColumnName("CreatedBy");
                entity.Property(e => e.UpdatedBy).HasColumnName("UpdatedBy");

                entity.Property(e => e.CompanyId).HasColumnName("CompanyId");

                // ===== Indexes =====
                entity.HasIndex(e => e.CompanyId).HasDatabaseName("IX_Formulas_CompanyId");
                entity.HasIndex(e => e.ProductId).HasDatabaseName("IX_Formulas_ProductId");
                entity.HasIndex(e => e.CreatedBy).HasDatabaseName("IX_Formulas_CreatedBy");
                entity.HasIndex(e => e.UpdatedBy).HasDatabaseName("IX_Formulas_UpdatedBy");
                entity.HasIndex(e => e.CheckBy).HasDatabaseName("IX_Formulas_CheckBy");
                entity.HasIndex(e => e.SentBy).HasDatabaseName("IX_Formulas_SentBy"); // ✅ sửa nhầm cũ (trước ghi CheckBy)


                // List/paging mặc định trong tenant (CreatedDate DESC)
                entity.HasIndex(e => new { e.CompanyId, e.IsActive, e.CreatedDate, e.ProductId })
                      .IsDescending(false, false, true, true)
                      .HasDatabaseName("IX_ProductId_Company_IsActive_CreatedDateDesc");

    
                // ===== Relationships =====
                entity.HasOne(d => d.Company).WithMany(p => p.Formulas)
                    .HasForeignKey(d => d.CompanyId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("FK_Formulas_Company");

                entity.HasOne(d => d.Product).WithMany(p => p.Formulas)
                    .HasForeignKey(d => d.ProductId)
                    .OnDelete(DeleteBehavior.Restrict) // ProductId là non-nullable ⇒ không SetNull
                    .HasConstraintName("FK_Formulas_Product");

                entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.FormulaCreatedByNavigations)
                    .HasForeignKey(d => d.CreatedBy)
                    .OnDelete(DeleteBehavior.SetNull)   // CreatedBy nullable
                    .HasConstraintName("FK_Formulas_CreatedBy");

                entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.FormulaUpdatedByNavigations)
                    .HasForeignKey(d => d.UpdatedBy)
                    .OnDelete(DeleteBehavior.SetNull)   // UpdatedBy nullable
                    .HasConstraintName("FK_Formulas_UpdatedBy");

                entity.HasOne(d => d.CheckByNavigation).WithMany(p => p.FormulaCheckByNavigations)
                    .HasForeignKey(d => d.CheckBy)
                    .OnDelete(DeleteBehavior.SetNull)   // CheckBy nullable
                    .HasConstraintName("FK_Formulas_CheckBy");

                entity.HasOne(d => d.SentByNavigation).WithMany(p => p.FormulaSentByNavigations)
                    .HasForeignKey(d => d.SentBy)
                    .OnDelete(DeleteBehavior.SetNull)   // SentBy nullable
                    .HasConstraintName("FK_Formulas_SentBy"); // ✅ sửa tên constraint (trước ghi nhầm IX_)
            });


            modelBuilder.Entity<FormulaMaterial>(entity =>
            {
                entity.HasKey(e => e.FormulaMaterialId).HasName("PK__FormulaM__0315C60A1F19742A");

                entity.ToTable("FormulaMaterials", "SampleRequests");

                entity.HasIndex(e => e.FormulaId, "IX_FormulaMaterials_FormulaId");

                entity.HasIndex(e => e.MaterialId, "IX_FormulaMaterials_MaterialId");

                entity.Property(e => e.FormulaMaterialId).HasDefaultValueSql("gen_random_uuid()");
                entity.Property(x => x.itemType).HasColumnName("item_type");

                entity.Property(e => e.ProductId).HasColumnName("product_id");
                entity.Property(x => x.Quantity).HasColumnName("quantity").HasPrecision(12, 10);
                entity.Property(x => x.UnitPrice).HasColumnName("unit_price").HasPrecision(22, 6);
                entity.Property(x => x.TotalPrice).HasColumnName("total_price").HasPrecision(22, 6);
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.MaterialNameSnapshot);
                entity.Property(e => e.MaterialExternalIdSnapshot);
                entity.Property(e => e.Unit);

                entity.HasOne(d => d.Product).WithMany(p => p.FormulaMaterials)
                    .HasForeignKey(d => d.ProductId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_FormulaMaterials_Product");

                entity.HasOne(d => d.Formula).WithMany(p => p.FormulaMaterials)
                    .HasForeignKey(d => d.FormulaId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_FormulaMaterials_Formula");

                entity.HasOne(d => d.Material).WithMany(p => p.FormulaMaterials)
                    .HasForeignKey(d => d.MaterialId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_FormulaMaterials_Material");

                entity.HasOne(d => d.Category).WithMany(p => p.FormulaMaterials)
                      .HasForeignKey(d => d.CategoryId)
                      .OnDelete(DeleteBehavior.Restrict)
                      .HasConstraintName("FK_FormulaMaterials_Category");
            });




            modelBuilder.Entity<MfgProductionOrdersPlan>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("PK__MfgProdu__3213E83F2188AAF5");

                entity.ToTable("MfgProductionOrdersPlan");

                entity.Property(e => e.Id)
                    .ValueGeneratedNever()
                    .HasColumnName("id");
                entity.Property(e => e.CreatedDate).HasColumnName("createdDate");
                entity.Property(e => e.ExpiryDate).HasColumnName("expiryDate");
                entity.Property(e => e.ExternalId)
                    .HasColumnType("citext")
                    .HasColumnName("externalId");
                entity.Property(e => e.ProductAddRate).HasColumnName("product_addRate");
                entity.Property(e => e.ProductCustomerExternalId)
                    .HasColumnType("citext")
                    .HasColumnName("product_customerExternalId");
                entity.Property(e => e.ProductExpiryType)
                    .HasColumnType("citext")
                    .HasColumnName("product_expiryType");
                entity.Property(e => e.ProductExternalId)
                    .HasColumnType("citext")
                    .HasColumnName("product_externalId");
                entity.Property(e => e.ProductId).HasColumnName("product_id");
                entity.Property(e => e.ProductMaxTemp).HasColumnName("product_maxTemp");
                entity.Property(e => e.ProductName)
                    .HasColumnType("citext")
                    .HasColumnName("product_name");
                entity.Property(e => e.ProductPackage)
                    .HasColumnType("citext")
                    .HasColumnName("product_package");
                entity.Property(e => e.ProductRecycleRate).HasColumnName("product_recycleRate");
                entity.Property(e => e.ProductRohsStandard).HasColumnName("product_rohsStandard");
                entity.Property(e => e.ProductWeight).HasColumnName("product_weight");
                entity.Property(e => e.Requirement)
                    .HasColumnType("citext")
                    .HasColumnName("requirement");
            });

          

            modelBuilder.Entity<Product>(entity =>
            {
                entity.ToTable("Products", "SampleRequests");
                entity.HasKey(e => e.ProductId).HasName("PK__Products__B40CC6CD344F4294");

                // ==== Columns (đặt HasColumnName y như property) ====
                entity.Property(e => e.ProductId)
                    .HasColumnName("ProductId")
                    .ValueGeneratedOnAdd()
                    .HasDefaultValueSql("gen_random_uuid()");

                entity.Property(e => e.ColourCode).HasColumnName("ColourCode").HasColumnType("citext");
                entity.Property(e => e.Name).HasColumnName("Name").HasColumnType("citext");
                entity.Property(e => e.ColourName).HasColumnName("ColourName");
                entity.Property(e => e.Additive).HasColumnName("Additive");
                entity.Property(e => e.UsageRate).HasColumnName("UsageRate");
                entity.Property(e => e.DeltaE).HasColumnName("DeltaE");
                entity.Property(e => e.Requirement).HasColumnName("Requirement");
                entity.Property(e => e.ExpiryType).HasColumnName("ExpiryType");
                entity.Property(e => e.StorageCondition).HasColumnName("StorageCondition");
                entity.Property(e => e.LabComment).HasColumnName("LabComment");
                //entity.Property(e => e.ProductType).HasColumnName("ProductType").HasMaxLength(100);
                entity.Property(e => e.Procedure).HasColumnName("Procedure");
                entity.Property(e => e.RecycleRate).HasColumnName("RecycleRate");
                entity.Property(e => e.TaicalRate).HasColumnName("TaicalRate");
                entity.Property(e => e.Application).HasColumnName("Application");
                entity.Property(e => e.ProductUsage).HasColumnName("ProductUsage");
                entity.Property(e => e.PolymerMatchedIn).HasColumnName("PolymerMatchedIn");
                entity.Property(e => e.Code).HasColumnName("Code");
                entity.Property(e => e.EndUser).HasColumnName("EndUser");

                entity.Property(e => e.FoodSafety).HasColumnName("FoodSafety");
                entity.Property(e => e.RohsStandard).HasColumnName("RohsStandard");
                entity.Property(e => e.ReachStandard).HasColumnName("ReachStandard");
                entity.Property(e => e.ReturnSample).HasColumnName("ReturnSample");
                entity.Property(e => e.IsRecycle).HasColumnName("IsRecycle").HasDefaultValue(false);

                entity.Property(e => e.MaxTemp).HasColumnName("MaxTemp");
                entity.Property(e => e.WeatherResistance).HasColumnName("WeatherResistance");
                entity.Property(e => e.LightCondition).HasColumnName("LightCondition");
                entity.Property(e => e.VisualTest).HasColumnName("VisualTest");

                entity.Property(e => e.OtherComment).HasColumnName("OtherComment");
                entity.Property(e => e.CategoryId).HasColumnName("CategoryId");
                entity.Property(e => e.Weight).HasColumnName("Weight");
                entity.Property(e => e.Unit).HasColumnName("Unit");


                entity.Property(e => e.CompanyId).HasColumnName("CompanyId");
                entity.Property(e => e.IsActive).HasColumnName("IsActive").HasDefaultValue(true);

                // ==== Indexes ====
                entity.HasIndex(e => e.CompanyId).HasDatabaseName("IX_Products_CompanyId");
                entity.HasIndex(e => e.CategoryId).HasDatabaseName("IX_Products_CategoryId");
                entity.HasIndex(e => e.CreatedBy).HasDatabaseName("IX_Products_CreatedBy");
                entity.HasIndex(e => e.UpdatedBy).HasDatabaseName("IX_Products_UpdatedBy");


                entity.HasIndex(e => new { e.ColourCode }, "UX_Products_ColourCode")
                    .IsUnique();

                // List/paging mặc định trong tenant (khớp CreatedDate desc)
                entity.HasIndex(e => new { e.CompanyId, e.IsActive, e.CreatedDate, e.ProductId })
                      .IsDescending(false, false, true, true)
                      .HasDatabaseName("IX_Products_Company_IsActive_CreatedDateDesc");

                // ==== Relationships ====
                entity.HasOne(d => d.Category).WithMany(p => p.Products)
                    .HasForeignKey(d => d.CategoryId)
                    .OnDelete(DeleteBehavior.SetNull)
                    .HasConstraintName("FK_Products_Category");

                entity.HasOne(d => d.Company).WithMany(p => p.Products)
                    .HasForeignKey(d => d.CompanyId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("FK_Products_Company");

                entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.ProductCreatedByNavigations)
                    .HasForeignKey(d => d.CreatedBy)
                    .OnDelete(DeleteBehavior.SetNull) // CreatedBy là non-nullable
                    .HasConstraintName("FK_Products_CreatedBy");

                entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.ProductUpdatedByNavigations)
                    .HasForeignKey(d => d.UpdatedBy)
                    .OnDelete(DeleteBehavior.SetNull) // UpdatedBy nullable
                    .HasConstraintName("FK_Products_UpdatedBy");

            });

         

            modelBuilder.Entity<SampleRequest>(entity =>
            {
                // Table + PK
                entity.ToTable("SampleRequests", "SampleRequests");
                entity.HasKey(e => e.SampleRequestId).HasName("PK_SampleRequests");

                // ==== Columns (HasColumnName cho tất cả) ====
                entity.Property(e => e.SampleRequestId).HasColumnName("SampleRequestId")
                     .ValueGeneratedOnAdd()
                     .HasDefaultValueSql("gen_random_uuid()");

                entity.Property(e => e.ExternalId).HasColumnName("ExternalId")
                    .HasMaxLength(50)
                    .IsRequired();

                entity.Property(e => e.CustomerId).HasColumnName("CustomerId");

                entity.Property(e => e.ManagerBy).HasColumnName("ManagerBy");

                entity.Property(e => e.ProductId).HasColumnName("ProductId");

                entity.Property(e => e.AttachmentCollectionId).HasColumnName("AttachmentCollectionId");

                entity.Property(e => e.RealDeliveryDate).HasColumnName("RealDeliveryDate");

                entity.Property(e => e.ExpectedDeliveryDate).HasColumnName("ExpectedDeliveryDate");

                entity.Property(e => e.RequestDeliveryDate).HasColumnName("RequestDeliveryDate");

                entity.Property(e => e.RequestTestSampleDate).HasColumnName("RequestTestSampleDate");

                entity.Property(e => e.ResponseDeliveryDate).HasColumnName("ResponseDeliveryDate");

                entity.Property(e => e.RealPriceQuoteDate).HasColumnName("RealPriceQuoteDate");

                entity.Property(e => e.ExpectedPriceQuoteDate).HasColumnName("ExpectedPriceQuoteDate");

                entity.Property(e => e.RequestType).HasColumnName("RequestType");

                entity.Property(e => e.ExpectedQuantity).HasColumnName("ExpectedQuantity");

                entity.Property(e => e.ExpectedPrice).HasColumnName("ExpectedPrice")
                    .HasPrecision(18, 4);

                entity.Property(e => e.SampleQuantity).HasColumnName("SampleQuantity");
                entity.Property(e => e.NumberDeliverySampleDate).HasColumnName("NumberDeliverySampleDate");

                entity.Property(e => e.OtherComment).HasColumnName("OtherComment");

                entity.Property(e => e.InfoType).HasColumnName("InfoType");

                entity.Property(e => e.FormulaId).HasColumnName("FormulaId");

                entity.Property(e => e.SaleComment).HasColumnName("SaleComment");
                entity.Property(e => e.AdditionalComment).HasColumnName("AdditionalComment");

                entity.Property(e => e.CustomerProductCode).HasColumnName("CustomerProductCode");
                entity.Property(e => e.Status).HasColumnName("Status")
                    .HasDefaultValue("New");
                entity.Property(e => e.Package).HasColumnName("Package")
                    .HasMaxLength(100);

                entity.Property(e => e.BagWeight).HasColumnName("BagWeight");


                entity.Property(e => e.CreatedBy).HasColumnName("CreatedBy");
                entity.Property(e => e.SendBy).HasColumnName("SendBy");
                ////entity.Property(e => e.SendByNameSnapshot).HasColumnName("SendByNameSnapshot")
                //    .HasMaxLength(255);
                entity.Property(e => e.UpdatedBy).HasColumnName("UpdatedBy");
                entity.Property(e => e.CompanyId).HasColumnName("CompanyId");
                entity.Property(e => e.IsActive).HasColumnName("IsActive")
                    .HasDefaultValue(true);

                // ==== Indexes ====
                //entity.HasIndex(e => e.CompanyId).HasDatabaseName("IX_SampleRequests_CompanyId");
                //entity.HasIndex(e => e.BranchId).HasDatabaseName("IX_SampleRequests_BranchId");
                entity.HasIndex(e => e.FormulaId).HasDatabaseName("IX_SampleRequests_FormulaId");
                entity.HasIndex(e => e.ManagerBy).HasDatabaseName("IX_SampleRequests_ManagerBy");
                entity.HasIndex(e => e.SendBy).HasDatabaseName("IX_SampleRequests_SendBy");
                entity.HasIndex(e => e.UpdatedBy).HasDatabaseName("IX_SampleRequests_UpdatedBy");
                entity.HasIndex(o => o.AttachmentCollectionId).HasDatabaseName("IX_SampleRequests_AttachmentCollection");

                // Trang list mặc định: Company + Active + sort theo thời gian (ổn định, ít tốn CPU sort)
                entity.HasIndex(e => new { e.CompanyId, e.IsActive, e.CreatedDate, e.SampleRequestId })
                      .IsDescending(false, false, true, true) // EF Core 8+: CreatedDate, SampleRequestId DESC
                      .HasDatabaseName("IX_SampleRequests_Company_IsActive_CreatedDateDesc");


                entity.HasIndex(e => new { e.CompanyId, e.IsActive, e.Status, e.CreatedDate })
                      .HasDatabaseName("IX_SampleRequests_Company_Status_CreatedDate");
                entity.HasIndex(e => new { e.CompanyId, e.IsActive, e.ExternalId })
                      .IsUnique()
                      .HasDatabaseName("UX_SampleRequests_Company_ExternalId");

                entity.HasIndex(e => new { e.CompanyId, e.ProductId })
                      .HasDatabaseName("IX_SampleRequests_Company_Product");

                entity.HasIndex(e => new { e.CompanyId, e.CustomerId })
                      .HasDatabaseName("IX_SampleRequests_Company_Customer");

                entity.HasIndex(e => new { e.CompanyId, e.CreatedBy })
                      .HasDatabaseName("IX_SampleRequests_Company_CreatedBy");

                // ==== Relationships ====
                entity.HasOne(d => d.AttachmentCollection)
                    .WithMany()
                    .HasForeignKey(d => d.AttachmentCollectionId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("FK_SampleRequests_AttachmentCollection");

                entity.HasOne(d => d.Company).WithMany(p => p.SampleRequests)
                    .HasForeignKey(d => d.CompanyId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("FK_SampleRequests_Company");

                entity.HasOne(d => d.Branch).WithMany(p => p.SampleRequestBranchs)
                    .HasForeignKey(d => d.BranchId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("FK_SampleRequests_BranchId");

                entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.SampleRequestCreatedByNavigations)
                    .HasForeignKey(d => d.CreatedBy)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("FK_SampleRequests_CreatedBy");

                entity.HasOne(d => d.Customer).WithMany(p => p.SampleRequests)
                    .HasForeignKey(d => d.CustomerId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("FK_SampleRequests_Customer");

                entity.HasOne(d => d.Formula).WithMany(p => p.SampleRequests)
                    .HasForeignKey(d => d.FormulaId)
                    .OnDelete(DeleteBehavior.SetNull)
                    .HasConstraintName("FK_SampleRequests_Formula");

                entity.HasOne(d => d.ManagerByNavigation).WithMany(p => p.SampleRequestManagerByNavigations)
                    .HasForeignKey(d => d.ManagerBy)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("FK_SampleRequests_Manager");

                entity.HasOne(d => d.Product).WithMany(p => p.SampleRequests)
                    .HasForeignKey(d => d.ProductId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("FK_SampleRequests_Product");

                entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.SampleRequestUpdatedByNavigations)
                    .HasForeignKey(d => d.UpdatedBy)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("FK_SampleRequests_UpdatedBy");

                entity.HasOne(d => d.SendByNavigation).WithMany(p => p.SampleRequestSendByNavigations)
                    .HasForeignKey(d => d.SendBy)
                    .OnDelete(DeleteBehavior.SetNull)
                    .HasConstraintName("FK_SampleRequests_SendBy");
            });



     

            modelBuilder.Entity<Unit>(entity =>
            {
                entity.HasKey(e => e.UnitId).HasName("PK__Units__44F5ECB5E080D698");

                entity.ToTable("Units", "Material");

                entity.HasIndex(e => e.CreatedBy, "IX_Units_CreatedBy");

                entity.Property(e => e.UnitId).HasDefaultValueSql("gen_random_uuid()");
                entity.Property(e => e.ExternalId).HasMaxLength(50);
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.Name).HasMaxLength(100);
                entity.Property(e => e.Symbol).HasMaxLength(20);

                entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.Units)
                    .HasForeignKey(d => d.CreatedBy)
                    .HasConstraintName("FK_Units_CreatedBy");
            });

    
        }
    }
}





