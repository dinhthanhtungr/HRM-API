using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Infrastructure.DatabaseContext.Configurations.DevandqaSchema
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    using HRM.Domain.Entities.DevandqaSchema;

    public class QcPassDetailHistoryConfiguration : IEntityTypeConfiguration<QcPassDetailHistory>
    {
        public void Configure(EntityTypeBuilder<QcPassDetailHistory> entity)
        {
            entity.ToTable("qc_pass_detail_history", "devandqa");

            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id)
                  .HasColumnName("id")
                  .UseIdentityAlwaysColumn();

            entity.Property(x => x.QcDate).HasColumnName("qcdate");
            entity.Property(x => x.MachineId).HasColumnName("machineid").HasColumnType("citext");
            entity.Property(x => x.BatchNo).HasColumnName("batchno").HasColumnType("citext");
            entity.Property(x => x.QcRound).HasColumnName("qcround");
            entity.Property(x => x.Note).HasColumnName("note");
            entity.Property(x => x.EmployeeId).HasColumnName("employeeid");
            entity.Property(x => x.StatusQc).HasColumnName("statusqc").HasColumnType("citext");

            entity.Property(x => x.Diameter).HasColumnName("diameter").HasPrecision(6, 2);
            entity.Property(x => x.DiameterResult).HasColumnName("diameterresult").HasPrecision(6, 2);
            entity.Property(x => x.ParticleSize).HasColumnName("particlesize").HasColumnType("citext");
            entity.Property(x => x.ParticleSizeResult).HasColumnName("particlesizeresult").HasPrecision(6, 2);
            entity.Property(x => x.EqualToStandard).HasColumnName("equaltostandard").HasColumnType("citext");
            entity.Property(x => x.ColorCode).HasColumnName("colorcode").HasColumnType("citext");
            entity.Property(x => x.DeltaE).HasColumnName("deltae").HasPrecision(6, 2);
            entity.Property(x => x.Moisture).HasColumnName("moisture").HasPrecision(6, 2);
            entity.Property(x => x.MoistureResult).HasColumnName("moistureresult").HasPrecision(6, 2);

            entity.Property(x => x.Mfr).HasColumnName("mfr").HasPrecision(6, 2);
            entity.Property(x => x.MfrResult).HasColumnName("mfrresult").HasColumnType("citext");

            entity.Property(x => x.FlexuralStrengthMpa).HasColumnName("flexuralstrengthmpa").HasPrecision(6, 2);
            entity.Property(x => x.FlexuralStrengthResult).HasColumnName("flexuralstrengthresult").HasColumnType("citext");

            entity.Property(x => x.ElongationMpa).HasColumnName("elongationmpa").HasPrecision(6, 2);
            entity.Property(x => x.ElongationResult).HasColumnName("elongationresult").HasColumnType("citext");

            entity.Property(x => x.HardnessShoreD).HasColumnName("hardnessshored").HasPrecision(6, 2);
            entity.Property(x => x.HardnessResult).HasColumnName("hardnessresult").HasColumnType("citext");

            entity.Property(x => x.DensitySperm3).HasColumnName("densitysperm3").HasPrecision(6, 2);
            entity.Property(x => x.DensityResult).HasColumnName("densityresult").HasColumnType("citext");

            entity.Property(x => x.TensileStrengthMpa).HasColumnName("tensilestrengthmpa").HasPrecision(6, 2);
            entity.Property(x => x.TensileStrengthResult).HasColumnName("tensilestrengthresult").HasColumnType("citext");

            entity.Property(x => x.FlexModulusMpa).HasColumnName("flexmodulusmpa").HasPrecision(6, 2);
            entity.Property(x => x.FlexModulusResult).HasColumnName("flexmodulusresult").HasColumnType("citext");

            entity.Property(x => x.ImpactKJPerM2).HasColumnName("impactkjperm2").HasPrecision(6, 2);
            entity.Property(x => x.ImpactResult).HasColumnName("impactresult").HasColumnType("citext");

            entity.Property(x => x.StaticOhm).HasColumnName("staticohm").HasPrecision(6, 2);
            entity.Property(x => x.StaticResult).HasColumnName("staticresult").HasColumnType("citext");

            entity.Property(x => x.StorageTempC).HasColumnName("storagetempc").HasPrecision(6, 2);
            entity.Property(x => x.StorageTempResult).HasColumnName("storagetempresult").HasColumnType("citext");

            entity.Property(x => x.FlagRound).HasColumnName("flaground");
            entity.Property(x => x.FlagBlackDot).HasColumnName("flagblackdot");
            entity.Property(x => x.FlagSwirl).HasColumnName("flagswirl");
            entity.Property(x => x.FlagDust).HasColumnName("flagdust");
            entity.Property(x => x.FlagWrongColor).HasColumnName("flagwrongcolor");

            entity.Property(x => x.PackingSpecKg).HasColumnName("packingspeckg").HasPrecision(6, 2);
            entity.Property(x => x.KeepSample).HasColumnName("keepsample");
            entity.Property(x => x.PrintSample).HasColumnName("printsample");

            // Index thực dụng
            entity.HasIndex(x => x.QcDate).HasDatabaseName("ix_qcpassdetailhistory_qcdate");
            entity.HasIndex(x => new { x.MachineId, x.QcDate })
                  .HasDatabaseName("ix_qcpassdetailhistory_machine_qcdate");
            entity.HasIndex(x => new { x.BatchNo, x.QcRound })
                  .HasDatabaseName("ix_qcpassdetailhistory_batch_round");
        }
    }

}
