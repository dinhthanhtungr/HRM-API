namespace HRM.Application.Features.DevAndQA.LabTests.Dtos;

/// <summary>Chỉ ID/mã/tên phục vụ lựa chọn trên form, không công khai thông tin tài khoản.</summary>
public sealed record LabTestLookupDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
}
public enum LabTestLookupKind { Supplier, LabEmployee, Material }
