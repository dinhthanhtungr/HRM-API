using System.Text.Json;
using HRM.Application.Commons.Models;
using HRM.Application.Features.DevAndQA.CoaCertificates.Commands.CreateCoaCertificate;
using HRM.Application.Features.DevAndQA.CoaCertificates.Commands.PatchCoaCertificate;
using HRM.Application.Features.DevAndQA.CoaCertificates.Commands.AddCoaCertificateResult;
using HRM.Application.Features.DevAndQA.CoaCertificates.Commands.PatchCoaCertificateResult;
using HRM.Application.Features.DevAndQA.EvaluationTemplates.Commands.CreateEvaluationTemplate;
using HRM.Application.Features.DevAndQA.EvaluationTemplates.Commands.PatchEvaluationTemplate;
using HRM.Application.Features.DevAndQA.IQC.Commands.CreateEvaluationStandard;
using HRM.Application.Features.DevAndQA.IQC.Commands.PatchEvaluationStandard;
using HRM.Application.Features.DevAndQA.IQC.Commands.CreateIqc;
using HRM.Application.Features.DevAndQA.IQC.Commands.PatchIqc;
using HRM.Application.Features.DevAndQA.IQC.Dtos;
using HRM.Application.Features.DevAndQA.IQC.Dtos.Evaluations;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.DevAndQA.Shared.Evaluations.Attachments;

internal sealed partial class EvaluationFileSaveService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private async Task<(object Record, Guid ParentId)> SaveRecordAsync(EvaluationFileResource resource, string? key,
        bool create, JsonElement payload, CancellationToken ct)
    {
        switch (resource)
        {
            case EvaluationFileResource.Iqc:
                OperationResult<GetQCInputByQC> qc;
                if (create)
                {
                    var input = Read<PostQCInputByQC>(payload);
                    var company = access.Company(true, HRM.Domain.Enums.Devandqa.EvaluationTemplateType.Iqc);
                    var exists = await db.QCInputByQCs.AnyAsync(x => x.VoucherDetailId == input.VoucherDetailId &&
                        x.WarehouseVoucherDetail.Voucher.CompanyId == company, ct);
                    qc = exists ? await sender.Send(new PatchIqcCommand(input.VoucherDetailId, Read<PatchQCInputByQC>(payload)), ct)
                        : await sender.Send(new CreateIqcCommand(input), ct);
                }
                else
                {
                    if (!long.TryParse(key, out var voucherId) || voucherId <= 0) throw new EvaluationRequestException("VoucherDetailId không hợp lệ.");
                    qc = await sender.Send(new PatchIqcCommand(voucherId, Read<PatchQCInputByQC>(payload)), ct);
                }
                RequireSuccess(qc);
                return (qc.Data!, qc.Data!.QCInputByQCId!.Value);
            case EvaluationFileResource.Coa:
                var coa = create ? await sender.Send(Read<CreateCoaCertificateCommand>(payload), ct)
                    : await sender.Send(Read<PatchCoaCertificateCommand>(payload) with { Id = GuidKey(key) }, ct);
                var updatedResults = coa.Results.ToList();
                if (payload.TryGetProperty("results", out var results))
                {
                    var changes = Read<List<IqcEvaluationSaveRow>>(results);
                    if (changes.Count > 200 || changes.Any(x => x is null) || changes.Select(x => x.EvaluationStandardId).Distinct().Count() != changes.Count)
                        throw new EvaluationRequestException("Danh sách kết quả COA không hợp lệ hoặc trùng chỉ tiêu.");
                    foreach (var change in changes)
                    {
                        var row = await db.CoaCertificateResults.Include(x => x.CoaSpecificationItem).FirstOrDefaultAsync(x => x.CoaCertificateId == coa.Id &&
                            (x.EvaluationStandardId == change.EvaluationStandardId || x.CoaSpecificationItem != null &&
                             x.CoaSpecificationItem.EvaluationStandardId == change.EvaluationStandardId), ct);
                        if (change.ResultId.HasValue && change.ResultId != row?.Id)
                            throw new EvaluationRequestException("ResultId không thuộc chỉ tiêu/phiếu COA này.");
                        if (change.SourceTemplateItemId.HasValue && change.SourceTemplateItemId != row?.SourceTemplateItemId)
                            throw new EvaluationRequestException("SourceTemplateItemId không hợp lệ.");
                        if (row is null)
                        {
                            if (change.ClearValue && change.Value is not null) throw new EvaluationRequestException("Không vừa gửi Value vừa clearValue.");
                            updatedResults.Add(await sender.Send(new AddCoaCertificateResultCommand(coa.Id, change.EvaluationStandardId,
                                change.Configuration, change.Value), ct));
                        }
                        else
                        {
                            var updatedRow = await sender.Send(new PatchCoaCertificateResultCommand(coa.Id, row.Id,
                                change.Value, change.ClearValue, change.Configuration), ct);
                            updatedResults.RemoveAll(x => x.Id == row.Id);
                            updatedResults.Add(updatedRow);
                        }
                    }
                }
                return (coa with { Results = updatedResults.OrderBy(x => x.Configuration.SortOrder).ToList() }, coa.Id);
            default: throw new EvaluationRequestException("Loại bản ghi không hợp lệ.");
        }
    }

    private static T Read<T>(JsonElement payload)
    {
        try { return payload.Deserialize<T>(JsonOptions) ?? throw new EvaluationRequestException("Payload không được null."); }
        catch (JsonException) { throw new EvaluationRequestException("Payload sai định dạng."); }
    }
    private static Guid GuidKey(string? key) => Guid.TryParse(key, out var id) && id != Guid.Empty
        ? id : throw new EvaluationRequestException("Id không hợp lệ.");
    private static void RequireSuccess<T>(OperationResult<T> result)
    {
        if (!result.Success || result.Data is null) throw new EvaluationRequestException(result.Message ?? "Không lưu được dữ liệu.");
    }
}
