# Đối chiếu DeliveryOrders cũ và HRM.api

Nguồn đối chiếu: `F:\Backend\VietausWebAPI.Core\Application\Features\DeliveryOrders`.
Thư mục này là module trong solution cũ, không phải một project độc lập đủ để copy và chạy.
Nó phụ thuộc IUnitOfWork/repository, AutoMapper, current user, Warehouse, Timeline,
formula rules, PDF/Excel renderer và entity ở các thư mục khác.

HRM.api đã có module Dispatch cùng các entity delivery trước lần đối chiếu này.
Bước một tái sử dụng module đó, thống nhất lượng phân bổ ở chọn đơn/create/update và sửa
tổng tồn khả dụng nhiều lot; không copy service nguyên khối hoặc tạo bảng delivery thứ hai.

## Bảng chức năng

| Chức năng legacy | Vị trí hiện tại / trạng thái |
| --- | --- |
| `GetAllAsync` | `Queries/GetDeliveryOrders`: list DTO, paging/filter và company scope. |
| `GetAsync` | `Queries/GetDeliveryOrderDetail`: detail DTO và visibility cost. |
| `GetSelectableLinesAsync` | `Queries/GetSelectableDeliveryLines`: PO còn lượng lập phiếu và lot khả dụng. |
| `CreateAsync` | `Commands/CreateDeliveryOrder`: tạo Pending, validate lượng/lot, lưu transaction. |
| `UpdateAsync` | `Commands/UpdateDeliveryOrder`: sửa Pending và thay bộ detail/lot active. |
| `SoftDeleteAsync` | `Commands/CancelDeliveryOrder`: dùng hủy theo lifecycle hiện tại, không sao chép rollback RealQuantity/warehouse của legacy. |
| `FinishAsync` | Legacy cập nhật **MerchandiseOrder**, không phải DeliveryOrder. Use case tương ứng ở `Features/PLM/SaleOrders/Commands/ConfirmSaleOrderDelivery`; quyền và điều kiện hiện tại khác legacy. |
| `GetAllDelivererAsync` | `Features/Dispatch/Deliverers/Queries/GetDeliverers`. |
| `CreateDelivererAsync` | Chưa chuyển. `DelivererInfor` hiện là danh mục không có CompanyId; cần chốt quản trị danh mục trước khi thêm mutation. |
| `DeliveryOrderPdfService.GenerateAsync` | `Queries/ExportDeliveryOrder` + `DeliveryOrderPdfRenderer`: PDF từng phiếu chỉ đọc, không chuyển side effect của lần in đầu. Có thêm `DeliveryOrderExcelRenderer` cho XLSX từng phiếu. |
| `BuildRowsAsync`, `BuildDeliveryFinishRowsAsync`, `BuildTransportWorkbookDataAsync` | Chưa chuyển báo cáo kế hoạch/hoàn tất/vận chuyển và Excel renderer. |

Contract cũ và mới không tương thích hoàn toàn: route/DTO/wrapper khác nhau; create hiện trả GUID,
FE tải detail sau tạo. Legacy có `LotNoList`; contract mới nhận lots có quantity riêng, hỗ trợ fallback
đã mô tả trong README. Đọc source không đồng nghĩa đã import dữ liệu lịch sử.

## Clean Architecture và điểm mở rộng

```text
HRM.Api/Controllers/Dispatch/DeliveryOrdersController.cs
  -> ISender (MediatR)
HRM.Application/Features/Dispatch/DeliveryOrders/
  Commands/<Action>/             command và handler riêng
  Queries/<Action>/              query và handler riêng
  Queries/DeliveryOrderQuantityQuery.cs  scope và lượng phân bổ dùng chung
  Dtos/                         contract API
  DeliveryOrder*Rules.cs         rule thuần: lifecycle, lot, tồn, quyền hiện có
  DeliveryOrderLotInventoryService.cs    đọc tồn/cost qua abstraction
HRM.Application/Abstractions/Persistence/Dispatch/
  IDispatchReadDbContext.cs / IDispatchWriteDbContext.cs
HRM.Infrastructure/DatabaseContext/
  ApplicationDbs/ApplicationDbContext.Delivery.cs
  Configurations/DeliverySchema/
HRM.Domain/Entities/DeliverySchema/ + Enums/Deliveries/
```

Application không tham chiếu Infrastructure; implementation EF ở Infrastructure cung cấp
context qua abstraction. Controller chỉ bind request, gọi use case và trả response.
Query lượng phân bổ là IQueryable để EF dịch SQL, không tải toàn bộ lịch sử giao về bộ nhớ.
Đổi rule lượng phải dùng chung cho selectable/create/update và cập nhật test cùng tài liệu.

## Những hành vi không được vô tình mang sang

PDF legacy không chỉ render: lần in đầu có thể cộng `MerchandiseOrderDetail.RealQuantity`,
đổi trạng thái dòng/PO, đánh dấu `HasPrinted` và ghi dữ liệu. Nếu chuyển exporter, phải xác định
riêng command xác nhận/in và cơ chế idempotency/transaction; không giấu mutation vào GET tải file.
Lượng phân bổ HRM.api đang dựa trên phiếu còn hiệu lực, không dựa trên lần in hoặc RealQuantity.

Legacy soft-delete còn rollback quantity và có phụ thuộc warehouse. Hủy trong HRM.api chỉ đổi
status; không ghép hai hành vi đó khi chưa có thiết kế thống nhất với kho.

Không thêm schema, seed quyền, backfill hay kết nối database cũ trong lần chuyển bước một.
Các entity trip/stop/vehicle đã tồn tại không có nghĩa là đã có API xếp chuyến/tuyến hoàn chỉnh.

## Nâng cấp tiếp

1. PDF/XLSX từng phiếu đã có projection DTO ở Application và renderer ở Infrastructure.
   Nếu cần mẫu ISO/logo giống legacy hoặc ghi nhận lần in, bổ sung contract riêng;
   chốt command xác nhận nghiệp vụ trước khi triển khai side effect legacy.
2. Excel: tách query số liệu và renderer; dùng cùng rule lượng, visibility và giới hạn ngày/số dòng.
3. Kho/chuyến: chốt mốc reserve/consume/release, khóa đồng thời, retry/idempotency và quyền quản trị
   người giao trước khi thêm transaction hoặc schema.
4. Dữ liệu cũ: lập mapping ID/company/status/lot, đối chiếu quantity, chạy dry-run trước import.
   Không suy ra lượng từng lot từ chuỗi nhiều lot; phải có nguồn phân bổ hoặc quy trình xử lý ngoại lệ.

## Verification cho bảo trì

```powershell
dotnet build HRM.Api\HRM.Api.csproj -p:OutDir=..\artifacts\verify-build\
dotnet test HRM.Application.Tests\HRM.Application.Tests.csproj --filter FullyQualifiedName~Features.Dispatch.DeliveryOrders -p:OutDir=..\artifacts\verify-tests\
git diff --check
```

Test lượng bao phủ Pending/Completed/legacy/canceled, company khác, detail/header inactive,
attachment, loại chính phiếu khi update và EF PostgreSQL SQL translation không kết nối DB.
Test tồn bao phủ reserve tổng chưa gắn lot và giới hạn tổng lot/product. Cần chạy checklist
database thực trong [DISPATCH-FLOW](DISPATCH-FLOW.md) trước triển khai tích hợp FE/kho.
