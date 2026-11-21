using HRM.Application.Commons.Patching;
using HRM.Application.Features.DevAndQA.LabTests.Dtos;
using HRM.Application.Features.DevAndQA.Shared.Evaluations;
using HRM.Domain.Entities.DevandqaSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.DevAndQA.LabTests.Services.Basic;

internal sealed partial class BasicLabTestService
{
    private async Task ApplySampleAsync(LabTest entity, BasicLabTestWriteRequest request, Guid company, CancellationToken ct)
    {
        if (request.ClearFields?.Contains("materialId") == true)
        {
            entity.MaterialId = null;
            entity.MaterialCode = null;
            entity.Material = null;
        }

        if (request.MaterialId is { } materialId && entity.MaterialId != materialId)
        {
            var material = await db.Materials.AsNoTracking()
                .Where(x => x.MaterialId == materialId && x.CompanyId == company && x.IsActive == true)
                .Select(x => new { x.ExternalId, x.Name })
                .SingleOrDefaultAsync(ct)
                ?? throw new EvaluationRequestException("NVL không hoạt động hoặc không thuộc công ty hiện tại.");

            if (string.IsNullOrWhiteSpace(material.Name) || material.Name.Trim().Length > 200)
                throw new EvaluationRequestException("NVL phải có tên hợp lệ, tối đa 200 ký tự.");

            // Chỉ snapshot khi chọn nguồn mới; lưu kết quả không làm đổi tên/mã lịch sử.
            entity.MaterialId = materialId;
            entity.MaterialCode = access.Normalize(material.ExternalId);
            entity.SampleName = material.Name.Trim();
            entity.Material = null;
            return;
        }

        if (entity.MaterialId.HasValue)
        {
            if (request.SampleName is not null && request.SampleName.Trim() != entity.SampleName)
                throw new EvaluationRequestException("Tên mẫu lấy từ NVL đã chọn; bỏ lựa chọn NVL để nhập mẫu tự do.");
            return;
        }

        if (request.SampleName is not null)
            PatchHelper.Set(request.SampleName.Trim(), () => entity.SampleName, x => entity.SampleName = x);
    }
}
