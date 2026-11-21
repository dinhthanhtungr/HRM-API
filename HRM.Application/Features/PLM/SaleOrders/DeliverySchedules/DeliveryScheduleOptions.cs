namespace HRM.Application.Features.PLM.SaleOrders.DeliverySchedules;

/// <summary>Rollout schema/backend độc lập với UI; mặc định giữ nguyên luồng ngày đơn.</summary>
public sealed class DeliveryScheduleOptions
{
    public const string SectionName = "DeliverySchedules";
    public bool StorageReady { get; set; }
    public bool EnableMultiScheduleUi { get; set; }
    public bool CanShowMultiScheduleUi => StorageReady && EnableMultiScheduleUi;
}
