using HRM.Application.Features.DevAndQA.IQC.Dtos.Evaluations;
namespace HRM.Application.Abstractions.Documents;
/// <summary>Xuất bảng đối chiếu kết quả động IQC từ snapshot; không đọc DB.</summary>
public interface IIqcEvaluationExcelRenderer
{
    byte[] Render(IReadOnlyList<IqcEvaluationExportColumn> columns);
}
