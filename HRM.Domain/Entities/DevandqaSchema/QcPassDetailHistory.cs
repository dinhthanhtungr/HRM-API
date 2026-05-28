using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Entities.DevandqaSchema
{
    public class QcPassDetailHistory
    {
        public long Id { get; set; }
        public DateTime QcDate { get; set; }
        public string? MachineId { get; set; }
        public string? BatchNo { get; set; }
        public int QcRound { get; set; }
        public string? Note { get; set; }
        public Guid EmployeeId { get; set; }
        public string? StatusQc { get; set; }

        public decimal? Diameter { get; set; }
        public decimal? DiameterResult { get; set; }
        public string? ParticleSize { get; set; }
        public decimal? ParticleSizeResult { get; set; }
        public string? EqualToStandard { get; set; }
        public string? ColorCode { get; set; }
        public decimal? DeltaE { get; set; }
        public decimal? Moisture { get; set; }
        public decimal? MoistureResult { get; set; }

        public decimal? Mfr { get; set; }
        public decimal? MfrResult { get; set; }

        public decimal? FlexuralStrengthMpa { get; set; }
        public string? FlexuralStrengthResult { get; set; }
        public decimal? ElongationMpa { get; set; }
        public string? ElongationResult { get; set; }
        public decimal? HardnessShoreD { get; set; }
        public string? HardnessResult { get; set; }

        public decimal? DensitySperm3 { get; set; }
        public decimal? DensityResult { get; set; }

        public decimal? TensileStrengthMpa { get; set; }
        public string? TensileStrengthResult { get; set; }
        public decimal? FlexModulusMpa { get; set; }
        public string? FlexModulusResult { get; set; }

        public decimal? ImpactKJPerM2 { get; set; }
        public string? ImpactResult { get; set; }
        public decimal? StaticOhm { get; set; }
        public string? StaticResult { get; set; }
        public decimal? StorageTempC { get; set; }
        public string? StorageTempResult { get; set; }

        public bool FlagRound { get; set; }
        public bool FlagBlackDot { get; set; }
        public bool FlagSwirl { get; set; }
        public bool FlagDust { get; set; }
        public bool FlagWrongColor { get; set; }

        public decimal? PackingSpecKg { get; set; }
        public bool KeepSample { get; set; }
        public bool PrintSample { get; set; }
    }

}
