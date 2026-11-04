# Chi tiết NCC và thời gian giao thực tế

`GET /api/v1/plm/material-suppliers/suppliers/{supplierId}?pageNumber=1&pageSize=15`

Giữ contract cũ: thông tin NCC, liên hệ, địa chỉ và vật tư phân trang. Yêu cầu đăng nhập; NCC active trong company hiện tại, không nhận companyId. Bổ sung `actualDelivery` ở header và mỗi `materials.items[]`, không thay `minDeliveryDays` (số ngày giao khai báo trên liên kết NCC/vật tư).

```json
{
  "actualDelivery": { "averageDays": 4.25, "sampleCount": 8, "missingDateCount": 2, "invalidDateCount": 1, "orderCount": 11 },
  "materials": {
    "items": [{ "materialCode": "NVL01", "minDeliveryDays": 3,
      "actualDelivery": { "averageDays": 2.5, "sampleCount": 2, "missingDateCount": 0, "invalidDateCount": 0, "orderCount": 2 }
    }], "totalCount": 1, "pageNumber": 1, "pageSize": 15
  }
}
```

Nguồn là PO active cùng NCC/company, không ở trạng thái Canceled/Cancelled. Bao gồm cả đơn đang giao, không yêu cầu Completed. Mốc bắt đầu là PurchaseOrder.CreateDate. Mốc giao đầu tiên là MIN QCInputByQC.CreatedDate qua IQC → dòng phiếu → phiếu nhập → yêu cầu kho, codeFromRequest trim khớp mã PO trim. Request phải active, request và voucher cùng company; chỉ loại phiếu ImportRawMaterial/ImportOther/ImportMaterial. Mã PO trống không được ghép. Không lọc theo quyết định QCPass/QCFail: đây là thời điểm lập IQC như ngày giao thực tế của màn PO, không phải thời điểm QC chấp nhận.

Màn PO đang hiển thị ngày IQC **mới nhất** (MAX), còn metric này dùng ngày **đầu tiên** (MIN) theo yêu cầu tính thời gian giao lần đầu. Không thay đổi ngày hiển thị PO hiện hành. Không dùng UpdatedDate, ngày Completed/audit, ngày giao nhập tay hay ngày ledger làm fallback. IQC mới hơn không đẩy ngày đầu về sau; nếu bổ sung IQC có ngày cũ hơn thì kết quả được tính lại theo dữ liệu hiện tại.

`averageDays` = trung bình `(firstActualDeliveryAt - createDate).TotalDays` của PO đủ mốc hợp lệ; làm tròn 2 chữ số AwayFromZero. Một ngày là 24 giờ, có phần lẻ, không phải ngày làm việc; không trừ cuối tuần/ngày nghỉ. Cùng thời điểm là 0 hợp lệ. Mỗi PO một mẫu, không trọng số kg/giá/số dòng. Header tính toàn bộ PO của NCC, độc lập keyword/trang vật tư. Mỗi vật tư chỉ tính PO có ít nhất một dòng active của vật tư đó, vật tư active cùng company; nhiều dòng cùng vật tư vẫn một mẫu. Mốc giao đầu dùng cấp PO, không phải ngày giao riêng từng dòng.

`sampleCount` là số PO đủ mốc hợp lệ; `missingDateCount` thiếu ngày tạo hoặc chưa có IQC liên kết; `invalidDateCount` đủ mốc nhưng IQC đầu trước ngày tạo PO. Tổng ba số bằng `orderCount`. Không có mẫu hợp lệ thì averageDays=null, không đổi thành 0; không có PO thì count=0. Chỉ tính metric từng vật tư cho trang hiện tại, không tải mọi trang vật tư. Các giá trị này tính khi query, không phải snapshot persist.

FE dùng nhãn “Số ngày giao lần đầu trung bình (thực tế)”, kèm số mẫu và số đơn thiếu ngày. Ngày lập IQC là proxy ngày giao thực tế theo quy ước PO hiện hành, không chứng minh thời điểm xe tới kho. Đơn legacy chưa có IQC không được suy ra ngày giả.

Quyền truy cập giữ như API detail NCC hiện hành; metric chỉ gồm thời gian/số lượng PO tổng hợp, không trả id PO, tiền hoặc payload audit. Foreign-company PO/voucher/request bị loại trong query. Không thêm migration, không sửa IQC, không đổi field giá cũ. Không thêm metric vào API danh sách NCC hoặc API catalog vật tư khác.
