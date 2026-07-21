namespace HRM.Application.Features.PLM.Formulas.Dtos.GetFormulas
{
    public sealed class FormulaList
    {
        public IReadOnlyList<FormulaId> FormulaSelects { get; set; } = [];
        public IReadOnlyList<FormulaId> FormulaDevs { get; set; } = [];
        public IReadOnlyList<FormulaId> FormulaStandard { get; set; } = [];
    }
}
