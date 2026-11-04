# Codex FE handoff — IQC

Tài liệu này dành riêng cho IQC, không sử dụng contract SampleRequests. Phạm vi: summary, detail, POST tạo QC, PATCH cập nhật QC. Đối chiếu source hiện tại; chưa xác nhận trên DB chạy thật. Không đổi API.

## Endpoint và quyền

Base URL chuẩn: `/api/v1/dev-and-qa/iqc`. Alias legacy `/api/iqcinput`; FE mới dùng route chuẩn.

| Chức năng | Method / route | HTTP thành công |
| --- | --- | --- |
| Summary | GET base URL | 200 |
| Detail | GET `/{voucherDetailId}` | 200 |
| Tạo QC | POST base URL | 200, không phải 201 |
| Cập nhật QC | PATCH `/{voucherDetailId}` | 200 |
| Excel tùy chọn | GET `/export-excel` | File XLSX |

Tất cả GET/POST/PATCH trên trả `{success,message,data}`. Dùng HTTP client đăng nhập hiện có. GET/list/detail/export cần `dev-and-qa.iqc.view`; POST/PATCH cần `dev-and-qa.iqc.manage`. Company lấy từ current user, không có companyId trong filter/payload. Mutation cần employee thuộc company. FE gate bằng capability hiện có; backend là nơi quyết định quyền.

Fallback role: QCUser/Admin/Developer/President xem+sửa; KHOUser chỉ xem. Không lấy quyền sửa PO làm quyền IQC. Thiếu capability/company/validation/mutation nghiệp vụ trả 400 với OperationResult thất bại; 401 khi chưa đăng nhập. Không mặc định mọi lỗi quyền đều 403.

**Id điều hướng và mutation là `voucherDetailId` (long), không phải `qcInputByQCId` (UUID).** Nếu FE có hạ tầng bigint/int64 thì dùng lại, tránh làm mất chính xác id vượt Number.MAX_SAFE_INTEGER.

## 1. Summary

```http
GET /api/v1/dev-and-qa/iqc?pageNumber=1&pageSize=15&hasQC=false&voucherDetailType=7
```

| Query | Ý nghĩa |
| --- | --- |
| `pageNumber` | Mặc định 1; không dương về 1 |
| `pageSize` | Mặc định 15; không dương về 20; không giả định có max 100 như module khác |
| `voucherDetailId` | Lọc dòng phiếu cụ thể; bỏ/0 = không lọc |
| `qcInputByQCId` | UUID QC mới nhất; không phải id route |
| `keyword` | Tìm mã NVL, tên NVL, lot, tên NCC; không tìm note/người QC trong list |
| `hasQC` | true = đã có QC; false = chưa có QC; bỏ = tất cả |
| `voucherDetailType` | 7/8/9/10 theo bảng quyết định dưới; enum hợp lệ ngoài nhóm này cho list rỗng |
| `fromDate`, `toDate` | Ngày ledger mới nhất theo dòng phiếu, hai đầu inclusive, ISO 8601 |

BE so trực tiếp timestamp `<= toDate`; nếu chọn một ngày cuối thì FE gửi thời điểm cuối ngày theo quy ước timezone app, không gửi 00:00 rồi kỳ vọng hết ngày. fromDate <= toDate. Không có sortBy/sortDirection; cố định voucherDetailId giảm dần. Keyword `%`/`_` giữ nghĩa wildcard legacy.

Response rút gọn:

```json
{
  "success": true,
  "message": null,
  "data": {
    "items": [{
      "voucherDetailId": 123,
      "externalId": "MAT-01", "name": "Nhựa nguyên sinh",
      "lotNumber": "LOT-01", "qtyKg": 250, "unit": "kg",
      "categoryName": "RESIN", "voucherType": 7,
      "requestCode": "", "inputDate": "2026-10-01T08:00:00",
      "hasQC": false, "qcInputByQCId": null,
      "qcEmployeeName": "", "qcCreatedDate": null,
      "qcDescription": "Tạm giữ"
    }],
    "totalCount": 1, "page": 1, "pageSize": 15, "totalPages": 1
  }
}
```

Response pagination dùng **`data.page`**, không phải pageNumber. Request vẫn gửi pageNumber. Không có hasNextPage/hasPreviousPage; FE tính từ page/totalPages. Với Axios, đọc `response.data.data.items`.

Mỗi row là dòng nhập kho. Cột gợi ý: mã/tên NVL, category, lot, QtyKg/unit, ngày nhập, đã QC, người/ngày QC, quyết định. Click row mở detail bằng voucherDetailId. Filter reset trang 1; debounce keyword và bỏ response lỗi thời. Loading/empty/error riêng. `hasQC=false` không đồng nghĩa QCFail; đó là chưa có bản ghi QC. `qcDescription` map loại dòng phiếu, không suy từ isSuccessQuality.

List chỉ lấy header ImportOther=9 và detail 7/8/9/10. Detail theo id không áp lại filter header này. Mã/tên/unit list ưu tiên Material hiện tại, fallback snapshot; category trống nếu không resolve. qtyKg/lot thuộc dòng phiếu. inputDate là MAX(WarehouseShelfLedger.CreatedAt) theo VoucherDetailId cùng company, giống lịch sử kho; chưa có ledger trả null, không fallback ngày tạo phiếu; qcCreatedDate là ngày tạo QC. requestCode hiện rỗng.

### Field summary đầy đủ

| JSON field | Kiểu |
| --- | --- |
| `voucherDetailId` | `long` |
| `externalId` | `string` |
| `name` | `string` |
| `lotNumber` | `string` |
| `qtyKg` | `decimal` |
| `unit` | `string` |
| `categoryName` | `string` |
| `voucherType` | `VoucherDetailType` |
| `requestCode` | `string` |
| `hasQC` | `bool` |
| `qcInputByQCId` | `Guid?` |
| `qcEmployeeName` | `string` |
| `qcCreatedDate` | `DateTime?` |
| `inputDate` | `DateTime?` |
| `qcDescription` | `string?` |

## 2. Detail và form

```http
GET /api/v1/dev-and-qa/iqc/123
```

Response minh họa chưa QC:

```json
{
  "success": true, "message": null,
  "data": {
    "voucherDetailId": 123,
    "materialExternalId": "MAT-01", "materialName": "Nhựa nguyên sinh",
    "lotNumber": "LOT-01", "qtyKg": 250, "unit": "kg",
    "supplierId": null, "supplierName": null, "supplierExternalId": null,
    "qcInputByQCId": null,
    "inspectionMethod": null, "isCOAProvided": null,
    "isMSDSTDSProvided": null, "isMetalDetectionRequired": null,
    "isSuccessQuality": null, "importWarehouseType": null, "note": null,
    "attachmentCollectionId": null, "attachmentStatus": 0,
    "attachmentLastError": null,
    "qcCreatedDate": null, "qcCreatedBy": null, "qcEmployeeName": null
  }
}
```

Phân biệt ba trạng thái:

- `success=true, data=null`: không tìm thấy dòng trong company; hiển thị không tìm thấy, không phải 404.
- data có context, `qcInputByQCId=null`: dòng tồn tại nhưng chưa QC; form tạo dùng POST nếu có manage.
- data có `qcInputByQCId`: có QC; form sửa dùng PATCH nếu có manage.

Context hiển thị read-only: material mã/tên, lot, QtyKg, unit, NCC. Detail material mã/tên lấy snapshot phiếu, unit kg; NCC resolve qua request/PO mới nhất cùng company. NCC null không phải lỗi form. QC lấy record CreatedDate mới nhất; audit là thông tin người tạo, không phải lịch sử chỉnh sửa.

Field editable: inspectionMethod, isCOAProvided, isMSDSTDSProvided, isMetalDetectionRequired, isSuccessQuality, importWarehouseType, note. Bool nullable dùng ba trạng thái Chưa khai báo/Có/Không; không tự đổi null thành false. `false` phải được gửi như giá trị hợp lệ. `isSuccessQuality` độc lập quyết định nhập; không suy quyết định từ bool này.

### Quyết định nhập

| Giá trị importWarehouseType | Code | Nhãn |
| --- | --- | --- |
| 7 | Waiter | Tạm giữ |
| 8 | QCPass | Chấp nhận |
| 9 | QCFail | Từ chối |
| 10 | Special | Chấp nhận đặc biệt |

JSON body gửi số enum 7/8/9/10 hoặc null khi chưa chọn; không gửi label/string enum. Summary voucherType có cùng giá trị cho bốn nhóm này. Clear quyết định QC không tự reset loại dòng phiếu đã lưu; FE refetch thay vì suy state.

### Field detail đầy đủ

| JSON field | Kiểu |
| --- | --- |
| `voucherDetailId` | `long` |
| `materialExternalId` | `string?` |
| `materialName` | `string?` |
| `lotNumber` | `string?` |
| `qtyKg` | `decimal?` |
| `unit` | `string?` |
| `supplierId` | `Guid?` |
| `supplierName` | `string?` |
| `supplierExternalId` | `string?` |
| `qcInputByQCId` | `Guid?` |
| `inspectionMethod` | `string?` |
| `isCOAProvided` | `bool?` |
| `isMSDSTDSProvided` | `bool?` |
| `isMetalDetectionRequired` | `bool?` |
| `isSuccessQuality` | `bool?` |
| `attachmentCollectionId` | `Guid?` |
| `attachmentStatus` | `AttachmentUploadStatus` |
| `attachmentLastError` | `string?` |
| `importWarehouseType` | `QcDecision?` |
| `note` | `string?` |
| `qcCreatedDate` | `DateTime?` |
| `qcCreatedBy` | `Guid?` |
| `qcEmployeeName` | `string?` |

## 3. POST tạo QC

```http
POST /api/v1/dev-and-qa/iqc
Content-Type: application/json
```

```json
{
  "voucherDetailId": 123,
  "inspectionMethod": "Đối chiếu COA và kiểm tra ngoại quan",
  "isCOAProvided": true,
  "isMSDSTDSProvided": false,
  "isMetalDetectionRequired": false,
  "isSuccessQuality": true,
  "importWarehouseType": 8,
  "note": "Đạt yêu cầu",
  "hasNewAttachments": false
}
```

voucherDetailId > 0 là bắt buộc. Các field QC nullable; không tự bịa validation bắt buộc toàn bộ checkbox/quyết định nếu business UI chưa thống nhất.

DTO có csName/csExternalId lưu metadata legacy; không phải supplierId. Có thể gửi từ NCC nếu giao diện cần lưu snapshot, nhưng **supplierName/supplierExternalId của GET lấy từ PO**, không lấy từ csName. materialName/materialExternalId có trong DTO POST nhưng BE tự lấy snapshot dòng phiếu; không cho FE sửa context bằng hai field này.

Nếu đã có QC, POST **bỏ qua giá trị mới, trả QC mới nhất đã lưu và đồng bộ PO lại**. Không dùng POST để sửa record. Sau response thành công luôn GET lại detail và reload summary; nếu form cũ tưởng chưa QC nhưng record đã được người khác tạo, không báo rằng giá trị form vừa gửi chắc chắn đã được lưu.

### Field POST đầy đủ

| JSON field | Kiểu |
| --- | --- |
| `voucherDetailId` | `long` |
| `csName` | `string?` |
| `csExternalId` | `string?` |
| `materialName` | `string?` |
| `materialExternalId` | `string?` |
| `inspectionMethod` | `string?` |
| `isCOAProvided` | `bool?` |
| `isMSDSTDSProvided` | `bool?` |
| `isMetalDetectionRequired` | `bool?` |
| `isSuccessQuality` | `bool?` |
| `importWarehouseType` | `QcDecision?` |
| `note` | `string?` |
| `attachmentCollectionId` | `Guid?` |
| `hasNewAttachments` | `bool?` |

## 4. PATCH cập nhật

```http
PATCH /api/v1/dev-and-qa/iqc/123
Content-Type: application/json
```

```json
{
  "isCOAProvided": false,
  "importWarehouseType": 10,
  "note": "Chấp nhận đặc biệt theo phê duyệt",
  "clearFields": ["inspectionMethod"]
}
```

Chỉ gửi field thực sự đổi. Path là voucherDetailId; không gửi QC UUID làm path. Field bỏ qua hoặc null giữ nguyên. false là cập nhật, không bỏ mất false khi dựng payload. Muốn xóa dùng clearFields; không dùng null làm tín hiệu xóa, không gửi chuỗi rỗng thay clearFields.

Whitelist exact/case-sensitive:

```json
[
  "inspectionMethod",
  "isCOAProvided",
  "isMSDSTDSProvided",
  "isMetalDetectionRequired",
  "isSuccessQuality",
  "importWarehouseType",
  "note"
]
```

Không dùng fieldCode product.* / sample_request.* từ tài liệu module khác. clearFields bỏ qua mặc định []; không gửi null. Không vừa gửi value vừa clear cùng field. Không clear attachmentCollectionId bằng whitelist này. PATCH không có expectedUpdatedDate/concurrency token; FE không tự thêm cơ chế đó vào payload.

PATCH khi chưa có QC trả 400. GET chọn QC mới nhất, PATCH legacy chọn record đầu tiên nếu dữ liệu có nhiều QC cùng dòng; không cung cấp UI chỉnh nhiều QC theo UUID. Refetch sau lưu để hiển thị kết quả canonical.

### Field PATCH đầy đủ

| JSON field | Kiểu |
| --- | --- |
| `clearFields` | `IReadOnlyList<string>` |
| `inspectionMethod` | `string?` |
| `isCOAProvided` | `bool?` |
| `isMSDSTDSProvided` | `bool?` |
| `isMetalDetectionRequired` | `bool?` |
| `isSuccessQuality` | `bool?` |
| `importWarehouseType` | `QcDecision?` |
| `note` | `string?` |
| `attachmentCollectionId` | `Guid?` |
| `hasNewAttachments` | `bool?` |

## 5. Mutation response và attachment

POST/PATCH trả OperationResult<GetQCInputByQC>, không phải UUID và không phải full context GET:

```json
{
  "success": true, "message": null,
  "data": {
    "voucherDetailId": 123,
    "qcInputByQCId": "11111111-1111-4111-8111-111111111111",
    "materialExternalId": "MAT-01", "materialName": "Nhựa nguyên sinh",
    "inspectionMethod": "Đối chiếu COA và kiểm tra ngoại quan",
    "isCOAProvided": true, "isMSDSTDSProvided": false,
    "isMetalDetectionRequired": false, "isSuccessQuality": true,
    "importWarehouseType": 8, "note": "Đạt yêu cầu",
    "attachmentStatus": 0, "attachmentLastError": null
  }
}
```

Ví dụ rút gọn. Mutation DTO thiếu context đầy đủ: supplier/lot/qty/audit có thể null/default. POST qcCreatedDate null; PATCH có CreatedDate của record. Không merge mutation DTO đè lên context hiện có; GET lại detail. GET detail trả AttachmentStatus/AttachmentLastError của QC mới nhất: None=0, Pending=1, Ok=2, Failed=3.

Payload tạo/sửa thông tin IQC là JSON. Upload ảnh/file dùng endpoint multipart riêng theo cùng kiểu SampleRequests:

| Thao tác | Method / route tương đối từ base IQC | Response |
| --- | --- | --- |
| Upload nhiều file | POST `/{voucherDetailId}/attachments?slot=9` | 201, array AttachmentDto trực tiếp |
| List | GET `/{voucherDetailId}/attachments` | 200, array AttachmentDto trực tiếp |
| Xem ảnh/file | GET `/{voucherDetailId}/attachments/{attachmentId}/content` | Stream file |
| Download | GET `/{voucherDetailId}/attachments/{attachmentId}/download` | Stream với filename |
| Xóa | DELETE `/{voucherDetailId}/attachments/{attachmentId}` | 204 |

Upload dùng FormData, append nhiều lần field **files**. Không tự set Content-Type vì browser phải tạo multipart boundary:

```typescript
const form = new FormData();
for (const file of selectedFiles) form.append('files', file);
await api.post(`/api/v1/dev-and-qa/iqc/${voucherDetailId}/attachments?slot=9`, form);
```

Lưu QC thành công trước khi upload; khi chưa có QC, upload trả 400, list trả [] nếu dòng phiếu tồn tại. POST QC thông thường tạo collection tự động; không cần FE tạo collection hoặc PATCH collectionId sau upload. Upload rồi xóa dùng manage; list/content/download dùng view. File ngoài collection/parent không được đọc/xóa. Khác company trả 404; thiếu quyền file trả 403. Slot sai/file không hợp lệ trả 400.

Slot cho phép: Photo=4, Specification=5, Other=6, QcReport=9. Mặc định QcReport: ảnh jpg/jpeg/png/webp/gif hoặc PDF, mỗi file tối đa 10 MiB theo rule chung; tổng request tối đa 50.000.000 byte gồm multipart overhead. Photo chỉ ảnh; tài liệu Word/Excel/text dùng Specification/Other theo MIME rule chung. Server kiểm tra extension/MIME và size trước lưu. Không hỗ trợ executable/HTML/SVG trong endpoint này.

AttachmentDto trả `attachmentId`, `attachmentCollectionId`, `slot`, `fileName`, `sizeBytes`, `url`, `downloadUrl`, `isImage`, `createDate`, `createBy`. Dùng isImage dựng thumbnail/gallery; file khác hiển thị icon+tên+dung lượng. URL thuộc route IQC có authorize; dùng HTTP client auth tải blob rồi tạo object URL khi app dùng Bearer token, không nhét token vào query URL. Revoke object URL khi unmount/đổi ảnh.

Upload thành công cập nhật status Ok; xóa file cuối chuyển None. Xóa là soft delete qua service chung. Upload lỗi giữ dữ liệu QC đã lưu; FE thông báo phần file chưa lưu và cho retry file, không POST lại QC để retry. Không có transaction nguyên tử giữa lưu file vật lý và cập nhật status; lỗi lưu status sau file cần GET list để đối chiếu trước retry. Upload không có idempotencyKey, khóa submit tránh trùng file. Sau upload/delete refetch list và detail. Không gửi hasNewAttachments=true khi chỉ xem file cũ.

Lưu quyết định có thể cập nhật loại dòng phiếu và lượng nhận/trạng thái PO trong transaction; backend thực hiện, FE không PATCH PO lần nữa. Stock sync vẫn tắt như nguồn. Không có live realtime contract IQC ở đây; invalidate/refetch query sau mutation.

## 6. Export và xử lý lỗi

GET `/export-excel` nhận filter như summary, không áp pagination. Thành công là blob XLSX, lấy filename từ Content-Disposition hoặc fallback; có hai sheet QCInput/BaoCao. Keyword sheet chi tiết rộng hơn summary nên số dòng có thể khác. Lỗi trả JSON OperationResult, cần phân biệt content-type trước download.

400 nghiệp vụ:

```json
{ "success": false, "message": "Không tìm thấy dữ liệu QC để cập nhật.", "data": null }
```

Hiển thị message backend. Model validation có thể là ProblemDetails/errors. Lỗi mạng/500 giữ draft; không tự retry mutation nhiều lần. Detail null xử lý riêng. Backend không có endpoint IQC summary tên `/summary`, không có PUT. JSON POST/PATCH QC khác với multipart upload file.

## Checklist triển khai Codex FE

1. Summary route GET base URL, wrapper data/page đúng; filter/search/pagination và state loading/empty/error.
2. Detail route long voucherDetailId; context read-only và form QC nullable.
3. Dùng qcInputByQCId null để chọn POST/PATCH; manage gate nút lưu, view gate trang.
4. Dropdown quyết định số 7/8/9/10; không suy quyết định từ bool.
5. PATCH gửi diff, false giữ đúng, clearFields whitelist, không gửi null để xóa.
6. Khóa submit lúc pending; thành công GET lại detail và invalidate summary/PO liên quan nếu app hiển thị.
7. Xử lý GET success/data-null, 400 business, 401, model validation và network error.

Nguồn: IQCController; Dtos/QCInputQuery, GetSummaryQCInput, GetQCInputByQC, PostQCInputByQC, PatchQCInputByQC; Services/Inspection/IqcReader, IqcWriter, IqcRules, IqcMutation. [README nghiệp vụ](README.md) mô tả chi tiết PO/export và khác biệt legacy.

Ngày nhập trong list và hai sheet Excel dùng chung nguồn ledger. Có bộ lọc ngày thì dòng chưa có ledger không khớp; không lọc ngày thì vẫn hiển thị với inputDate=null. Ngày tạo QC không đổi.
