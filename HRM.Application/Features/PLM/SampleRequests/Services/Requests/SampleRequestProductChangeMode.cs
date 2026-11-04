namespace HRM.Application.Features.PLM.SampleRequests.Rules;

/// <summary>
/// Xác định luồng mặc định khi Sale thay đổi field Product từ màn hình Sample Request.
/// </summary>
internal enum SampleRequestProductChangeMode
{
    DirectNotify = 1,
    RequireLabApproval = 2
}
