# IQC — kiểm tra nguyên vật liệu đầu vào

## Yêu cầu đổi hàng DDH liên quan

POST `/api/v1/dev-and-qa/iqc/{voucherDetailId}/purchase-order/request-exchange` không có body.
Yêu cầu authenticated/ManageIqc, company và employee hợp lệ. Tìm DDH active cùng company qua
dòng phiếu kho → phiếu kho.RequestId → yêu cầu kho active.codeFromRequest → DDH.ExternalId.
Không nhận ID DDH hoặc status tùy ý từ FE. Chuyển DDH sang `ExchangeRequested` (FE hiển thị “Y/C đổi hàng”),
ghi UpdatedBy/UpdatedDate trong transaction; không thay quyết định QC, số lượng hoặc trạng thái lệnh kho.
Cho phép DDH Completed; DDH Canceled trả 409. Gọi lại idempotent, không đổi audit.
Lưu QC sau đó vẫn tính RealQuantity nhưng không ghi đè ExchangeRequested. API complete/cancel của Purchasing
vẫn là thao tác chủ động theo contract riêng. Không bắt buộc đã lưu QC để gọi API này.

Response 200: `{"success":true,"message":null,"data":{"purchaseOrderId":"<guid>","externalId":"DDH_...","status":"ExchangeRequested"}}`.
Status là dữ liệu đã lưu tại DDH, không phải trạng thái đánh giá IQC. FE refresh DDH sau thành công.
400: ID không hợp lệ; 403: thiếu quyền/company/employee; 404: dòng phiếu hoặc DDH không tồn tại
trong scope; 409: DDH đã hủy. Lỗi trả wrapper success=false/message. Không thêm migration.

ACCUser được cấp dev-and-qa.iqc.view như President; không cấp thêm dev-and-qa.iqc.manage. Query tiếp tục kiểm tra company/record scope. Với permission DB, chạy seed claim rồi login/refresh để token nhận quyền xem.

Tài liệu Codex FE: [Summary, detail, POST và PATCH IQC](FE_HANDOFF.md).

Feature chuyển nghiệp vụ đang chạy từ `QCInputByQCFeatures` của hệ thống cũ. Folder feature là `DevAndQA/IQC`; entity `QCInputByQC`, bảng `devandqa.QCInputByQC` và tên field legacy được giữ nguyên, không đổi schema.

## Kiến trúc và phạm vi

- `Commands/CreateIqc`, `Commands/PatchIqc`: use case ghi, gọi `IqcWriter` trong Application.
- `Queries/GetIqcSummary`, `Queries/GetIqcDetail`, `Queries/ExportIqcExcel`: use case đọc riêng, dùng selector của `IqcReader`.
- `IqcMutation`, `IqcRules`, `IqcReportCalculator`: PATCH, mapping quyết định, trạng thái PO và thống kê thuần.
- `IIqcDbContext`: persistence abstraction trong Application; Infrastructure implement bằng DbContext hiện có.
- `IIqcExcelRenderer`: abstraction tài liệu trong Application; ClosedXML và layout workbook ở Infrastructure dưới `Documents/Excels/IQC`.
- `IQCController`: nhận request, gọi MediatR, trả DTO/file.

Tất cả endpoint yêu cầu `[Authorize]` và company context hợp lệ. QC được scope qua `WarehouseVoucherDetail → WarehouseVoucher.CompanyId`; request, PO, vật tư, category và nhân viên được scope riêng cùng company. POST/PATCH yêu cầu employee thuộc company hiện tại. Quyền đọc/list/detail/export là `dev-and-qa.iqc.view`; quyền POST/PATCH là `dev-and-qa.iqc.manage`. Fallback: QCUser/Admin/Developer/President được xem và sửa; KHOUser chỉ xem; SaleUser/LabUser không được cấp quyền mặc định. Endpoint từ chối khi không có capability, không trả DTO rút gọn. Đây là gate bảo mật bổ sung cho repo đích; không tái sử dụng quyền sửa PO để cấp quyền QC. Scope company vẫn bắt buộc dù có capability.

Source of truth quyền là Identity role claim `permission`; fallback nằm trong ApplicationPermissionRoleSets/ApplicationRoleSets cho token chưa dùng permission DB. Baseline seed/rollback hiện có được bổ sung hai capability (không tạo migration mới và không chạy SQL). Môi trường dùng explicit permission set phải cập nhật claim hai quyền trước rollout; đăng nhập/refresh token sau cập nhật vì quyền là snapshot trong token. Feature không trả giá/cost của PO hoặc entity EF.

## Cách đọc code nghiệp vụ

Services được nhóm theo nghiệp vụ:

```text
Services/
├── Inspection/
│   ├── IqcReader.cs
│   ├── IqcReader.List.cs
│   ├── IqcReader.Detail.cs
│   ├── IqcWriter.cs
│   ├── IqcMutation.cs
│   └── IqcRules.cs
├── Reporting/
│   ├── IqcReader.Export.cs
│   └── IqcReportCalculator.cs
└── PurchaseOrders/
    └── IqcPurchaseOrderSync.cs
```

`Inspection` chứa đọc danh sách/chi tiết, create/PATCH và quy tắc kiểm tra. `Reporting` chứa chuẩn bị dữ liệu hai sheet export và tính thống kê. `PurchaseOrders` chứa đồng bộ kết quả IQC sang PO.

Các phần `IqcReader` vẫn thuộc cùng partial class, dùng chung company scope trong `Inspection/IqcReader.cs`. Namespace `HRM.Application.Features.DevAndQA.IQC.Services` được giữ để các nơi gọi hiện tại không phải đổi; folder thể hiện nhóm trách nhiệm. `IqcWriter.cs` chứa trọn luồng create/PATCH và helper attachment.

Mỗi command/query public và handler vẫn ở file riêng theo cấu trúc repo; XML summary giải thích mục đích command/query và service. Khi nhóm nghiệp vụ mới có use case độc lập, tạo feature riêng với Commands/Queries/Dtos tương ứng. Tổ chức lại folder không đổi public contract hoặc nghiệp vụ.

## API

Base route chuẩn: `/api/v1/dev-and-qa/iqc`. Alias tương thích: `/api/iqcinput`. Các action legacy vẫn dùng được trên cả hai base route.

| Method | Route tương đối | Ý nghĩa |
| --- | --- | --- |
| GET | `/` hoặc `/GetPaged` | Danh sách dòng nhập cần/đã QC, phân trang |
| GET | `/{voucherDetailId}` hoặc `/GetByVoucherDetailId?voucherDetailId=...` | Context dòng phiếu và kết quả QC mới nhất |
| POST | `/` | Tạo IQC; nếu đã có thì trả QC mới nhất và đồng bộ lại PO |
| PATCH | `/{voucherDetailId}` hoặc `/PatchByVoucherDetailId/{voucherDetailId}` | Cập nhật QC và đồng bộ PO |
| GET | `/export-excel` | Xuất workbook `QCInput` và `BaoCao`, không phân trang |

GET/POST/PATCH trả `OperationResult<T>` (`success`, `message`, `data`). Lỗi validation trả HTTP 400; lỗi hệ thống đi qua middleware hiện có, không đưa nội dung exception DB ra response. Detail không tìm thấy trong company trả `success=true, data=null`, không tiết lộ record ngoài company. File thành công trả XLSX với tên `QCInputByQC_yyyyMMdd_HHmmss.xlsx`.

### Danh sách và bộ lọc

Query nhận `pageNumber` (mặc định 1), `pageSize` (mặc định 15), `voucherDetailId`, `qcInputByQCId`, `keyword`, `hasQC`, `voucherDetailType`, `fromDate`, `toDate`. Số trang không dương về 1; kích thước không dương về 20 như nguồn. Khoảng phân trang vượt `int` bị từ chối.

Chỉ list/export dòng có **header `VoucherType = ImportOther (9)`** và detail thuộc `Waiter (7)`, `QCPass (8)`, `QCFail (9)`, `Special (10)`. Filter enum hợp lệ nhưng ngoài bốn loại này trả danh sách rỗng. Detail/POST/PATCH theo id giữ cách truy cập của nguồn: kiểm tra dòng thuộc company, không áp lại filter header của list.

Ngày lọc và `inputDate` là **MAX(WarehouseShelfLedger.CreatedAt) theo VoucherDetailId trong company hiện tại**, giống lịch sử phiếu kho, hai đầu inclusive. Không fallback sang ngày tạo header. Dòng chưa có ledger trả null và không khớp khi có fromDate/toDate; không lọc ngày thì vẫn giữ dòng. QC mới nhất là bản ghi có `CreatedDate` lớn nhất; `hasQC` và `qcInputByQCId` lọc bản ghi này. Sắp xếp theo `VoucherDetailId` giảm dần. Keyword list/report chỉ tìm mã NVL, tên NVL, lot và tên NCC bằng PostgreSQL `ILIKE`. Keyword sheet chi tiết Excel còn tìm tên người QC, `CSName`, `CSExternalId`, `Note`, giữ đúng khác biệt nguồn; keyword chứa `%`/`_` giữ semantics wildcard legacy.

Response list mẫu rút gọn:

```json
{
  "success": true,
  "data": {
    "items": [{
      "voucherDetailId": 123,
      "externalId": "MAT-01",
      "name": "Nhựa nguyên sinh",
      "lotNumber": "LOT-01",
      "qtyKg": 250,
      "unit": "kg",
      "categoryName": "RESIN",
      "voucherType": 7,
      "requestCode": "",
      "inputDate": "2026-10-01T08:00:00",
      "hasQC": false,
      "qcInputByQCId": null,
      "qcCreatedDate": null,
      "qcEmployeeName": "",
      "qcDescription": "Tạm giữ"
    }],
    "totalCount": 1,
    "page": 1,
    "pageSize": 15,
    "totalPages": 1
  }
}
```

`qtyKg`, lot và `voucherType` lấy từ detail. List ưu tiên mã/tên/unit hiện tại của Material tìm bằng `ProductCode = ExternalId`, fallback mã/tên snapshot của detail; unit fallback chuỗi rỗng. `categoryName` lấy Material.Category và rỗng khi không resolve được. `inputDate` là ngày ledger mới nhất của dòng phiếu; `qcCreatedDate` là ngày tạo QC. Không có QC: id/date `null`, người kiểm chuỗi rỗng, `hasQC=false`. `qcDescription` map từ **detail.VoucherType**, không từ bool `IsSuccessQuality`. `requestCode` vẫn rỗng như selector nguồn.

### Detail và mutation response

Detail vẫn trả context ngay cả khi chưa có QC:

```json
{
  "success": true,
  "data": {
    "voucherDetailId": 123,
    "materialExternalId": "MAT-01",
    "materialName": "Nhựa nguyên sinh",
    "lotNumber": "LOT-01",
    "qtyKg": 250,
    "unit": "kg",
    "supplierId": "00000000-0000-0000-0000-000000000001",
    "supplierName": "Nhà cung cấp A",
    "qcInputByQCId": null,
    "inspectionMethod": null,
    "importWarehouseType": null,
    "attachmentCollectionId": null,
    "attachmentStatus": 0,
    "qcCreatedDate": null
  }
}
```

Mã/tên/lot/khối lượng detail là snapshot phiếu kho, unit luôn `kg`. NCC resolve qua voucher.RequestId → request.codeFromRequest → PO.ExternalId, chọn PO active có CreateDate mới nhất; dữ liệu NCC là realtime cùng company. Nhóm QC lấy bản ghi mới nhất đã lưu; bool nullable `null` là chưa khai báo kết quả, `false` là kiểm tra âm tính. Id QC `null` nghĩa chưa có bản ghi persist, context không phải QC đã tạo.

POST/PATCH trả DTO mutation legacy: snapshot mã/tên, field QC, collection và trạng thái upload. Các field context lot/qty/supplier/người QC không được điền, FE cần GET detail để lấy context đầy đủ. POST không điền `qcCreatedDate`; PATCH điền ngày tạo gốc của QC, không phải ngày sửa. **GET detail giữ selector nguồn: `attachmentStatus` mặc định `None (0)` và `attachmentLastError=null`; trạng thái tracking thật chỉ có trong DTO mutation.** Không dùng field này trên GET detail để kết luận upload đã hoàn tất.

## POST, PATCH và attachment

POST nhận DTO `PostQCInputByQC`; `voucherDetailId > 0` bắt buộc. `materialName/materialExternalId` nhận để tương thích nhưng server lấy snapshot từ detail. `CSName/CSExternalId` lưu như nguồn. Guid QC và collection mới dùng UUID v7; ngày/người tạo lấy server/current employee.

POST nếu đã có QC bỏ qua toàn bộ input còn lại, không tạo record mới, đồng bộ lại PO theo quyết định đã lưu. PATCH chọn bản ghi đầu tiên theo voucher detail như nguồn (không tự dọn dữ liệu trùng); **không khóa sửa sau kết luận**, vì gate Waiter của nguồn đang bị comment.

PATCH dùng contract repo đích:

- Không gửi field hoặc gửi `null`: giữ nguyên.
- Gửi value: cập nhật, bao gồm `false` và chuỗi rỗng.
- Xóa: `clearFields` dùng whitelist `inspectionMethod`, `isCOAProvided`, `isMSDSTDSProvided`, `isMetalDetectionRequired`, `isSuccessQuality`, `importWarehouseType`, `note`.
- Không được vừa gửi value vừa clear cùng field; không clear id/collection/audit/snapshot.
- Clear quyết định đặt QC về null, giữ detail.VoucherType như nhánh nullable nguồn; lượng được QC chấp nhận của dòng này sẽ không được cộng vào PO.

Khi tạo luôn đảm bảo collection. `hasNewAttachments=true` đặt trạng thái Pending và xóa lỗi upload; PATCH tạo collection nếu chưa có. `AttachmentCollectionId=Guid.Empty` được bỏ qua (POST tự tạo mới). Collection được gửi khác collection đang gắn phải chứng minh thuộc company qua một IQC parent hiện có; orphan/collection khác company bị từ chối. Đây là check bảo mật bổ sung vì bảng collection không có CompanyId. Luồng được chuyển không upload file hay tự chuyển Pending sang Complete; helper cập nhật trạng thái trong repository nguồn không có consumer nên không thêm endpoint mới.

## Đồng bộ phiếu kho và PO

Khi có quyết định: `Waiter/QCPass/QCFail/Special` map 1–1 sang detail.VoucherType (7/8/9/10). Khi PATCH đổi quyết định sang value khác mới cập nhật loại detail. **Không cập nhật WarehouseShelfStock**: call sync stock bị tắt trong nguồn và vẫn giữ tắt.

Truy vết PO bằng request active cùng company, trim code PO. Chọn PO active đầu tiên khớp ExternalId như nguồn; chọn dòng active khớp `MaterialExternalIDSnapshot`, ưu tiên cùng `LineNo`. Tính `RealQuantity` bằng tổng `QtyKg` của mọi dòng nhập khớp mã PO và mã NVL có QC `QCPass` hoặc `Special`; không phân bổ theo LineNo/lot. Join QC vẫn giữ tất cả bản ghi nguồn, nên QC legacy trùng có thể cộng lặp khối lượng. Khi create chưa lưu QC, thay quyết định rỗng của dòng hiện tại bằng quyết định mới; khi PATCH lưu QC trước rồi mới tính.

PO status trên tất cả dòng active: mọi dòng nhận đủ/vượt RequestQuantity → `Completed`; có dòng nhận > 0 → `InProgress`; còn lại → `Pending`. Nullable quantity được tính bằng 0; không có dòng active thì không đổi status/audit. PO audit cập nhật employee và DateTime.Now. Không resolve được PO/dòng PO thì lưu QC bình thường, không tự tạo liên kết.

QC, loại detail và cập nhật PO nằm trong một transaction Serializable. Có lock theo company trong một process; xung đột serialization giữa nhiều process được trả qua xử lý lỗi hiện hành, client có thể retry. Không thêm unique constraint/index/migration.

API PO hiển thị `PurchaseOrderDetail.RealQuantity` đã lưu bởi IQC và tính tiền thực nhận theo lượng QC chấp nhận. Warehouse ledger chỉ phục vụ ngày nhập/báo cáo nhập kho; không thay thế kết quả QC trên summary/detail PO. Xem [Purchasing](../../Purchasing/PurchaseOrders/README.md) để phân biệt lượng QC trên PO và lượng vật lý trong Excel.

## Excel

Sheet `QCInput` giữ 12 cột và layout nguồn. Cột “Khối lượng giao hàng” và “Khối lượng thực nhận” **đều dùng QtyKg thực nhận** như helper nguồn, không dùng `DeliveryQuantityKg`. Ngày kiểm lấy ngày tạo QC; cột Ngày nhập lấy ngày ledger mới nhất của dòng phiếu. ResultText ưu tiên quyết định QC (Special/Pass/Fail/Waiter), sau đó fallback bool IsSuccessQuality; khi không QC để trống.

Sheet `BaoCao` giữ hai nhóm thống kê: category và trạng thái detail. Số mã là distinct ExternalId không trắng trong từng nhóm; khối lượng cộng mọi dòng; tỷ lệ theo số mã của nhóm/tổng số mã nhóm, làm tròn 1 chữ số bằng Math.Round mặc định. Category trim và uppercase, trống thành `KHÁC`. Trạng thái luôn có bốn dòng theo thứ tự HÀNG ĐẠT/HÀNG LỖI/CHỜ QC/CHẤP NHẬN ĐẶC BIỆT; nhóm không có dữ liệu có 0. Dòng tổng tỷ lệ vẫn ghi 100 kể cả dataset rỗng như helper nguồn.

Selector report giữ mã/tên snapshot, unit `kg`; điền InputDate từ ledger giống list; QCDescription giữ trống như trước. Filter export không áp pagination. Do keyword chi tiết rộng hơn keyword report, số dòng của hai sheet có thể khác nhau; giữ behavior nguồn.

## Kiểm chứng và khác biệt có chủ đích

Test trong `HRM.Application.Tests/Features/DevAndQA/IQC` kiểm tra mapping, POST lặp, snapshot, attachment scope, PATCH clear/giữ nguyên, chuyển QC và PO, rollback orchestration, cách thống kê, layout/cột workbook và translation SQL PostgreSQL cho detail/export. Test store không thay thế integration DB; SQL translation không kết nối DB. Cần smoke test trên dữ liệu thật trước rollout, nhất là dữ liệu QC trùng và request/PO code trùng.

Khác biệt với nguồn: bắt buộc đăng nhập, capability xem/sửa độc lập và company/employee scope; kiểm tra collection parent; PATCH không tự xóa nullable bị bỏ qua; mutation atomic; validation enum/date/id, loại dữ liệu inactive và không trả exception DB; pagination response giữ field `page` legacy. Route legacy, tên DTO field, quyết định, công thức và layout Excel được giữ; không tuyên bố HTTP/security/PATCH tương thích tuyệt đối 1–1.

## Chỉ tiêu động — cấu trúc mới

Mô hình tiêu chuẩn và nơi lưu key/value theo phiếu IQC được mô tả tại [DYNAMIC-CRITERIA.md](DYNAMIC-CRITERIA.md). API động được bổ sung riêng; API nhập/xem/xuất IQC hiện tại giữ nguyên.


## Ảnh và file IQC

Service `Services/Attachments/IqcAttachmentService` dùng `IAttachmentService` và storage chung như SampleRequests; controller riêng `IqcAttachmentsController`. Lưu QC trước rồi upload file vào collection của QC mới nhất, không tạo bảng/migration. Detail project trạng thái upload và lỗi thực tế.

Các endpoint tại `/api/v1/dev-and-qa/iqc/{voucherDetailId}/attachments`: POST multipart field `files` (201 array DTO), GET list (200 array), DELETE `/{attachmentId}` (204), GET `/{attachmentId}/content` và `/download`. Slot mặc định QcReport=9; cho phép thêm Photo=4, Specification=5, Other=6 theo rule chung và whitelist extension/MIME. Tổng request tối đa 50.000.000 byte; QcReport/Photo mỗi file tối đa 10 MiB. Danh sách khi chưa QC là []; upload cần bản ghi QC.

Upload/delete cần manage và employee cùng company; list/content/download cần view. Parent thuộc company, file phải thuộc collection của QC; collection có liên kết QC khác company hoặc InternalMail bị từ chối. URL DTO được đổi sang route IQC để check parent khi xem/download. Upload thành công cập nhật Ok; xóa file cuối chuyển None. Delete là soft delete. Lưu file và status không nguyên tử: nếu status save thất bại sau upload, FE refetch list trước retry để tránh trùng. Không tự xóa dữ liệu QC khi upload lỗi. Chi tiết FE xem [FE_HANDOFF.md](FE_HANDOFF.md).


## API danh mục tiêu chuẩn đánh giá

Danh mục `EvaluationStandard` có GET list/detail, POST và PATCH tại `/api/v1/dev-and-qa/iqc/evaluation-standards`. Dùng ViewIqc/ManageIqc và company/employee scope; ngưng sử dụng bằng isActive=false. API template và kết quả động xem Shared/Evaluations/API.md. Contract và ví dụ payload tại [EVALUATION_STANDARDS_API.md](EVALUATION_STANDARDS_API.md). Không tạo/chạy migration hoặc thay database trong task API.


## Cấu hình mặc định trong danh mục chỉ tiêu

EvaluationStandard bổ sung DataType, Unit, TestMethod, InputConfigJson, AcceptanceRuleJson, SpecificationText, IsRequired. API danh mục đọc/ghi các field này, validate schema theo kiểu và dùng PatchHelper. Factory copy mặc định sang Item IQC/COA mới, không thay đổi snapshot đã lưu. API template/nhập kết quả đã được bổ sung tại Shared/Evaluations/API.md. Contract và hướng dẫn rollout SQL cột mới tại [EVALUATION_STANDARDS_API.md](EVALUATION_STANDARDS_API.md); SQL chưa được chạy vào DB.

## Template chung và snapshot từng phiếu

Phần lịch sử riêng của IQC đặt tại `Services/Evaluations/IqcEvaluationHistoryReader.cs`, DTO tại `Dtos/Evaluations/IqcEvaluationHistoryDto.cs`; handler không gọi EvaluationApiService để đọc lịch sử. `IqcEvaluationMapper` dùng chung giữa chi tiết/ghi/lịch sử IQC để giữ cùng mapping snapshot và validation nguồn legacy.

Lịch sử kết quả cùng mặt hàng dùng API riêng `GET /{voucherDetailId}/evaluation/history?pageNumber=1&pageSize=3`, phân trang theo phiếu QC, cùng ProductCode/company, loại dòng đang mở. Response có ngày tạo QC, mã lô và evaluation snapshot, không phải các lần PATCH. Xem [contract lịch sử](../Shared/Evaluations/API.md#lịch-sử-qc-cùng-mặt-hàng-api-riêng-phân-trang).

Kết quả động là tùy chọn. Mở phiếu gọi GET `/{voucherDetailId}/evaluation`: có đánh giá thì trả snapshot IsStored=true, chưa có thì preview mẫu mặc định IsStored=false (chưa có QC vẫn xem được). Không có template trả preview rỗng, không ngăn QC thông thường. GET không ghi DB và FE không cần POST khởi tạo khi mở màn hình.

POST/PATCH QC nhận field evaluation để lưu số đo bổ sung trong cùng transaction với QC/attachment/PO. Không gửi hoặc không nhập Value thì không tạo đánh giá mới. Dòng preview gửi StandardId/SourceTemplateItemId, dòng đã lưu gửi ResultId; sau lưu tải lại GET để lấy ID thật. Payload và semantics chi tiết xem [API động](../Shared/Evaluations/API.md#lưu-qc-và-kết-quả-bổ-sung-cùng-transaction).

Danh mục phương pháp đo, template IQC/COA và cấu hình đã lưu trên từng phiếu được mô tả tại [Shared/Evaluations](../Shared/Evaluations/README.md). Đã có API động; xem [endpoint và payload](../Shared/Evaluations/API.md). DB deployment chưa thực hiện.

## Cách đọc phần đánh giá IQC

- Query/Command → handler → IqcEvaluationService.
- Services/Evaluations/IqcEvaluationService.cs: đọc snapshot hoặc preview, khởi tạo chủ động, thêm/PATCH dòng.
- Services/Evaluations/IqcEvaluationService.Save.cs: validate toàn bộ payload rồi stage kết quả trong transaction của IqcWriter.
- Services/Evaluations/IqcEvaluationHistoryReader.cs: lịch sử QC cùng mặt hàng, phân trang theo phiếu.
- Services/Evaluations/IqcEvaluationMapper.cs: mapping snapshot và validation nguồn legacy dùng trong IQC.
- Dtos/Evaluations: DTO và payload chỉ thuộc IQC.

Không tạo evaluation khi chỉ mở màn hình. Lưu chung QC/kết quả do IqcWriter giữ transaction; service stage không lock/Save/Commit riêng. Scope, capability và request contract giữ nguyên; xem API.md của Shared/Evaluations.

## Xuất Excel kết quả tiêu chuẩn

GET `/api/v1/dev-and-qa/iqc/{voucherDetailId}/evaluation/export-excel?pageNumber=1&pageSize=3` trả file XLSX, yêu cầu authenticated/ViewIqc và company scope. PageSize từ 1 đến 50; PageNumber từ 1. Xuất phiếu hiện tại trước, tiếp theo là một trang lịch sử gần nhất theo đúng semantics API history cùng ProductCode. Chỉ xuất snapshot đã lưu, không xuất preview hoặc dữ liệu chưa submit; chưa có đánh giá/dòng đã lưu trả 409. Ngoài company trả 404. Không có lịch sử vẫn xuất cột hiện tại.

Bố cục một sheet: logo Vietaus từ Pdf:Quotation:LogoPath hoặc asset Assets/Pdf/VietAusLogo.png, header metadata màu vàng, hàng Chỉ tiêu (Tiếng Việt)/Test item (English)/Unit/Test method/Results màu xanh, viền mảnh. Ngày là QC.CreatedDate; tên/mã/lô lấy dòng phiếu kho hiện tại, không phải snapshot metadata. Có các chỉ tiêu chỉ tồn tại trong lịch sử; ghép StandardId, legacy thiếu ID ghép CriterionCode. Tên chỉ tiêu tách hai cột: tiếng Việt từ CriterionName, tiếng Anh từ CriterionNameEn của snapshot. Thiếu tên tiếng Anh hiển thị —, không tự dịch hoặc lấy tên tiếng Việt thay thế. Unit/phương pháp khác snapshot chuẩn được ghi ngay trong ô kết quả, không quy đổi. Null/thiếu kết quả là —; 0/false giữ nguyên. Không tự tô kết quả vàng hoặc suy ra pass/fail. Logo thiếu là lỗi cấu hình cần xử lý, không dựng logo giả. API cũ `/iqc/export-excel` giữ nguyên.

FE gọi API với auth hiện có, nhận Blob và tên từ Content-Disposition; không parse response thành JSON khi thành công. Lỗi trả JSON theo mã HTTP. File luôn phản ánh dữ liệu đã lưu; cần lưu form trước khi xuất dữ liệu đang nhập. Không có schema/migration/DB write trong luồng xuất.

## Quyền LabUser

LabUser có ViewIqc/ManageIqc: xem/tạo/sửa QC, lưu đánh giá, lịch sử, upload/xóa file và xuất Excel trong company. Danh mục tiêu chuẩn: được tạo/sửa tên, nameEn và cấu hình; mã sau tạo không được đổi, POST vẫn nhận code do FE gửi. Template Draft được sửa nội dung đúng loại nhưng LabUser không được tạo/đổi mã; template Published/Retired giữ ngoại lệ Developer. Chưa chạy seed DB; token permission-model=1 phải có claims tương ứng.


## Attachment theo chỉ tiêu

[Luồng lưu kết quả Attachment cùng QC và file](../Shared/Evaluations/ATTACHMENT_SAVE_API.md)

## Đồng bộ result đã lưu

[Contract đồng bộ snapshot theo Current/Selected/Company](RESULT_CATALOG_SYNC_API.md).
Thao tác chủ động thay cấu hình lịch sử từ danh mục; giữ giá trị đo và tệp, có preview/token và transaction.

## Ngày nhập từ ledger

List và cả hai sheet Excel dùng cùng ngày nhập ledger (scoped company), không lấy WarehouseVoucher.CreatedDate.
Filter fromDate/toDate áp lên ngày này trước count/pagination. Ví dụ đầu phiếu tạo tháng 9, ledger nhập tháng 10:
phiếu thuộc bộ lọc tháng 10. QC CreatedDate và thứ tự chọn QC mới nhất không thay đổi.
`inputDate:null` nghĩa là chưa có ledger liên quan, không có ngày giả từ header. Response list/route/query giữ nguyên;
Excel chi tiết giữ tên property nội bộ CreatedDate nhưng giá trị là ngày nhập ledger, header hiển thị Ngày nhập.
Nếu một dòng có nhiều ledger, dùng ngày mới nhất đúng semantics lịch sử kho; không tự lọc tăng/giảm hoặc chọn lần đầu.
ToDate vẫn so trực tiếp <= timestamp; FE gửi cuối ngày nếu muốn lấy trọn ngày, không thay đổi timezone.
Không ghi lại ngày trong DB, không migration. GET detail/mutation không phát sinh field ngày nhập mới.
