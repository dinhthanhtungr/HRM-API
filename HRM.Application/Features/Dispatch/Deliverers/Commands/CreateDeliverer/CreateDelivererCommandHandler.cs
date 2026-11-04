using HRM.Application.Abstractions.Persistence.Dispatch;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Domain.Entities.DeliverySchema;
using MediatR;

namespace HRM.Application.Features.Dispatch.Deliverers.Commands.CreateDeliverer;

internal sealed class CreateDelivererCommandHandler(IDispatchWriteDbContext db, ICurrentUser user)
    : IRequestHandler<CreateDelivererCommand, OperationResult<Guid>>
{
    public async Task<OperationResult<Guid>> Handle(CreateDelivererCommand request, CancellationToken ct)
    {
        // Danh mục không có CompanyId: quản trị toàn hệ thống, không suy diễn tenant scope.
        if (!user.IsAuthenticated || !user.IsInRole(ApplicationRoles.Admin))
            return OperationResult<Guid>.Fail("Chỉ Admin được tạo người giao trong danh mục dùng chung.");
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 200 ||
            request.DelivererType?.Length > 100 || request.Phone?.Length > 50 || request.Note?.Length > 2000)
            return OperationResult<Guid>.Fail("Tên người giao hoặc độ dài thông tin không hợp lệ.");
        var entity = new DelivererInfor
        {
            Id = Guid.CreateVersion7(), Name = request.Name.Trim(), DelivererType = request.DelivererType?.Trim(),
            Phone = request.Phone?.Trim(), Note = request.Note?.Trim(), IsActive = true
        };
        db.DelivererInfors.Add(entity);
        await db.SaveChangesAsync(ct);
        return OperationResult<Guid>.Ok(entity.Id, "Tạo người giao hàng thành công.");
    }
}
