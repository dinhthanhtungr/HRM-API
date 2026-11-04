# Tệp đính kèm NVL trong Mua hàng

NVL phải được lưu trước. FE không gửi collectionId, storage path hoặc companyId. Tất cả route yêu cầu đăng nhập, capability `purchasing.catalog.manage` và NVL active thuộc company hiện tại; chỉ có quyền PO không đủ. Upload/xóa còn yêu cầu EmployeeId active cùng company. Không thêm bảng/cột/migration. Route PLM đọc cũ giữ contract riêng. Controller collection chung và route `/api/v1/attachments/{id}` từ chối đọc/ghi tệp thuộc NVL bằng HTTP 400, kể cả NVL inactive: phải dùng endpoint theo materialId để kiểm tra parent/company. Job import nội bộ vẫn chạy qua service, không bị chặn bởi controller.

| Method | Route | Thành công |
| --- | --- | --- |
| GET | `/api/v1/purchasing/materials/{materialId}/attachments` | 200 danh sách metadata active, không phân trang |
| GET | `/api/v1/purchasing/materials/{materialId}/attachments/{attachmentId}/content` | 200 stream inline, hỗ trợ range |
| GET | Cùng route content với `?mode=download` | Stream với tên tải xuống |
| POST | `/api/v1/purchasing/materials/{materialId}/attachments?slot=MaterialTds` | 201 metadata và phiên bản NVL mới |
| DELETE | `/api/v1/purchasing/materials/{materialId}/attachments/{attachmentId}?expectedUpdatedDate=...` | 200 metadata còn lại và phiên bản NVL mới |

## Danh sách và response mutation

Trả trực tiếp, không có wrapper OperationResult:

```json
{
  "materialId": "00000000-0000-0000-0000-000000000001",
  "updatedDate": "2026-10-21T10:00:00.000001",
  "attachments": [{
    "attachmentId": "00000000-0000-0000-0000-000000000002",
    "slot": "MaterialTds",
    "fileName": "TDS.pdf", "sizeBytes": 123456,
    "isImage": false, "isPdf": true,
    "contentUrl": "/api/v1/purchasing/materials/00000000-0000-0000-0000-000000000001/attachments/00000000-0000-0000-0000-000000000002/content",
    "downloadUrl": "/api/v1/purchasing/materials/00000000-0000-0000-0000-000000000001/attachments/00000000-0000-0000-0000-000000000002/content?mode=download",
    "createdDate": "2026-10-21T10:00:00"
  }]
}
```

Chưa có collection/tệp trả attachments=[]; không tự tạo collection khi GET. Danh sách gồm tệp active sẵn có (kể cả tài liệu legacy) theo CreateDate tăng dần rồi AttachmentId. createdDate là ngày tạo attachment, không phải ngày NVL. Không trả bytes/base64/storage path/collectionId. Xem/tải xác nhận attachmentId thuộc collection của đúng materialId trước khi mở storage; file không tồn tại/đã ngừng/khác parent trả 404. API không nhận slot để tùy ý thay metadata tệp cũ.

## Upload

Multipart form, các phần file cùng tên `files`; token `expectedUpdatedDate` gửi dưới dạng text ISO chính xác nhận từ API NVL hoặc list tệp. Không làm tròn/mất microsecond.

```javascript
const body = new FormData();
body.append('files', file);
if (material.updatedDate != null)
  body.append('expectedUpdatedDate', material.updatedDate);
// POST .../attachments?slot=MaterialTds với body và cơ chế xác thực hiện tại.
// Không tự đặt multipart Content-Type: browser phải tạo boundary.
```

Slot được phép: Photo=4 (chỉ ảnh), MaterialTds=12, MaterialMsds=13, MaterialCoa=14, MaterialCertificate=15, MaterialOther=16 (ảnh/PDF). Nếu không truyền slot hoặc slot khác trả 400. Nhận JPG/JPEG, PNG, GIF, WebP, BMP và PDF; kiểm tra phần mở rộng, MIME và chữ ký đầu tệp tương ứng. Không nhận SVG/HTML/Office ở endpoint upload này, dù legacy có thể chứa các định dạng khác. Không tuyên bố kiểm tra header là quét virus hoặc xác minh toàn bộ cấu trúc tài liệu.

Giới hạn từng file theo AttachmentRules: Photo 10 MiB, slot tài liệu 50 MiB; toàn request tối đa 50.000.000 byte gồm multipart overhead, nên tổng file phải nhỏ hơn giới hạn request. Cho nhiều file, không nhận file rỗng; không deduplicate nội dung. Tên file lưu metadata được lấy basename; storage tạo tên an toàn theo implementation dùng chung.

Upload tự tạo/gắn collection nếu thiếu, trong transaction cùng metadata/audit. Khóa catalog theo company trước khi đọc phiên bản NVL để tuần tự hóa mutation catalog. Khi lưu một phần file thất bại, rollback DB và dọn các file vật lý đã ghi; lỗi dọn được log để vận hành xử lý. DB và file share không có distributed transaction nên không bảo đảm dọn sau process crash/mất kết nối lúc commit.

## Xóa và concurrency

Xóa mềm IsActive=false, không xóa vật lý và không có route hard-delete ở bộ API này. Tệp không thuộc NVL hoặc đã xóa trả 404. NVL/collection dùng chung giữa nhiều NVL bị từ chối sửa để không tác động parent khác; collection dùng chung khác company không được đọc.

Upload và xóa thành công luôn tăng Material.UpdatedDate (độ chính xác microsecond, tăng tối thiểu 1 microsecond) và UpdatedBy là employee thực hiện. Token expectedUpdatedDate phải bằng phiên bản đang lưu; stale hoặc thiếu token khi NVL đã có UpdatedDate trả 409. Legacy UpdatedDate=null cho phép expected null; mutation đầu tiên tạo phiên bản mới. FE cập nhật token NVL từ response sau mỗi mutation, kể cả khi đồng thời mở form master. Hai phiên upload với token cũ không được cùng ghi đè. Không dùng attachment.createdDate làm token.

403 thiếu quyền/company/employee; 404 parent/file không tồn tại hoặc khác scope; 400 file/slot/collection dùng chung không hợp lệ; 409 phiên bản cũ; request quá lớn có thể bị host/proxy chặn trước controller. Không có upload staging cho NVL chưa lưu: FE lưu master trước, lấy materialId/updatedDate, rồi upload.

## Verification

Targeted tests kiểm tra quyền catalog so với PO-only/unauthenticated, employee khác company, parent khác company/inactive, IDOR file, token stale, tạo collection/audit/URL, xóa mềm, MIME giả, slot sai, file quá lớn và dọn file sau lỗi storage giữa batch. Fake store không thay thế kiểm thử HTTP multipart, storage thật và hai phiên concurrent trên PostgreSQL triển khai.
