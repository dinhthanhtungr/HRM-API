using HRM.Application.Features.Auth.Contracts;

namespace HRM.Application.Abstractions.Authentication;

public interface IIdentityAuthenticationService
{

    /// <summary>
    /// Kiểm tra thông tin đăng nhập bằng username hoặc email và password.
    /// Nếu hợp lệ, trả về thông tin user đã xác thực kèm roles, EmployeeId và CompanyId.
    /// Nếu sai tài khoản, sai mật khẩu hoặc user bị khóa thì trả về null.
    /// </summary>
    Task<AuthenticatedUserDto?> ValidateUserAsync(
        string userNameOrEmail,
        string password,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lưu refresh token mới cho user sau khi đăng nhập thành công.
    /// Refresh token này dùng để cấp lại access token khi access token hết hạn.
    /// </summary>
    Task StoreRefreshTokenAsync(
        Guid userId, 
        string refreshToken, 
        DateTime expiresAtUtc, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Kiểm tra refresh token có tồn tại và còn hạn hay không.
    /// Nếu hợp lệ, trả về thông tin user đã xác thực kèm roles, EmployeeId và CompanyId.
    /// Nếu token rỗng, không tồn tại hoặc hết hạn thì trả về null.
    /// </summary>
    Task<AuthenticatedUserDto?> ValidateRefreshTokenAsync(
        string refreshToken, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Thu hồi refresh token của user.
    /// Thường dùng khi user đăng xuất hoặc cần vô hiệu hóa phiên đăng nhập.
    /// </summary>
    Task RevokeRefreshTokenAsync(
        Guid userId, 
        CancellationToken cancellationToken = default);

}
