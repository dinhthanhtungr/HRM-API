using HRM.Domain.Entities.BomSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs;

public partial class ApplicationDbContext
{
    public virtual DbSet<BomDefinition> BomDefinitions { get; set; } = default!;
    public virtual DbSet<BomVersion> BomVersions { get; set; } = default!;
    public virtual DbSet<BomVersionItem> BomVersionItems { get; set; } = default!;
    public virtual DbSet<ManufacturingBomStage> ManufacturingBomStages { get; set; } = default!;
    public virtual DbSet<ManufacturingBomStageMachine> ManufacturingBomStageMachines { get; set; } = default!;
    public virtual DbSet<ManufacturingBomStageMachineParameter> ManufacturingBomStageMachineParameters { get; set; } = default!;
    public virtual DbSet<ManufacturingBomStageTransition> ManufacturingBomStageTransitions { get; set; } = default!;
    public virtual DbSet<ManufacturingProcessTemplate> ManufacturingProcessTemplates { get; set; } = default!;
    public virtual DbSet<ManufacturingProcessTemplateApplicability> ManufacturingProcessTemplateApplicabilities { get; set; } = default!;
    public virtual DbSet<ManufacturingProcessTemplateStage> ManufacturingProcessTemplateStages { get; set; } = default!;
    public virtual DbSet<ManufacturingProcessTemplateStageMachine> ManufacturingProcessTemplateStageMachines { get; set; } = default!;
    public virtual DbSet<ManufacturingProcessTemplateStageMachineParameter> ManufacturingProcessTemplateStageMachineParameters { get; set; } = default!;
    public virtual DbSet<ManufacturingProcessTemplateStageTransition> ManufacturingProcessTemplateStageTransitions { get; set; } = default!;
    public virtual DbSet<ManufacturingWorkInstructionTemplate> ManufacturingWorkInstructionTemplates { get; set; } = default!;
    public virtual DbSet<ManufacturingWorkInstructionChecklistItem> ManufacturingWorkInstructionChecklistItems { get; set; } = default!;
    public virtual DbSet<ManufacturingBomStageWorkInstruction> ManufacturingBomStageWorkInstructions { get; set; } = default!;
    public virtual DbSet<ManufacturingBomStageChecklistItem> ManufacturingBomStageChecklistItems { get; set; } = default!;
    public virtual DbSet<ManufacturingLossType> ManufacturingLossTypes { get; set; } = default!;
    public virtual DbSet<ManufacturingLossProfile> ManufacturingLossProfiles { get; set; } = default!;
    public virtual DbSet<ManufacturingLossProfileRule> ManufacturingLossProfileRules { get; set; } = default!;
    public virtual DbSet<ManufacturingBomLossRule> ManufacturingBomLossRules { get; set; } = default!;
    public virtual DbSet<BomVersionItemAlternative> BomVersionItemAlternatives { get; set; } = default!;
    public virtual DbSet<ProductStandardBomVersion> ProductStandardBomVersions { get; set; } = default!;
}
