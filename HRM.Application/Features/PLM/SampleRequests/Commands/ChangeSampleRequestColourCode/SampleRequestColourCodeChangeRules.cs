using HRM.Domain.Enums.SampleRequests;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.ChangeSampleRequestColourCode;

internal static class SampleRequestColourCodeChangeRules
{
    public const int MaxColourCodeLength = 100;

    public static bool IsEditableStatus(string? status)
        => string.Equals(status, SampleRequestStatus.New.ToString(), StringComparison.OrdinalIgnoreCase) ||
           string.Equals(status, SampleRequestStatus.Pending.ToString(), StringComparison.OrdinalIgnoreCase) ||
           string.Equals(status, SampleRequestStatus.InProgress.ToString(), StringComparison.OrdinalIgnoreCase);

    public static string BuildMessage(string oldColourCode, string newColourCode)
        => $"Mã màu đã được cập nhật từ {oldColourCode} thành {newColourCode}.";

    public static string BuildNotificationTitle(string newColourCode)
        => $"Mã màu đã đổi thành {newColourCode}";
}
