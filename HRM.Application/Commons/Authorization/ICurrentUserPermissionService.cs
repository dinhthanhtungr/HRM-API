namespace HRM.Application.Commons.Authorization;

/// <summary>
/// Kiểm tra capability của current user mà không để feature phụ thuộc trực tiếp vào tên role.
/// </summary>
public interface ICurrentUserPermissionService
{
    bool HasPermission(string permission);
}
