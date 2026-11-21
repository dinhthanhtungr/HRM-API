# Customer Labels

API `/api/v1/plm/customer-labels` quản lý mẫu nhãn theo cặp `CustomerId` và `ProductId`.
Mọi endpoint yêu cầu đăng nhập. API chỉ truy cập header khi cả Customer và Product đều thuộc
company hiện tại; tạo và cập nhật yêu cầu policy `PLM.ProductTechnicalInfo.Edit`.

## Mã nhãn trước và sau ColorCode

- Sale được tạo/lưu nhãn khi sản phẩm chưa có ColorCode. BE lấy mã KH từ Customer đã qua company/active scope; không dùng mã KH hoặc ColorCode do client gửi làm nguồn chính.
- Chưa có ColorCode: cấp mã `KH_375_1`, `KH_375_2`... bằng `IExternalIdService.GenerateGlobalCodeAsync`, counter hiện có được tách theo company + CustomerId. Counter nguyên tử của PostgreSQL chống trùng khi lưu đồng thời và không tái sử dụng số khi mã tạm đã được thay thế; số có thể khuyết khi một lần lưu thất bại.
- Có ColorCode: dùng mã hiện hành của Product. Hai luồng Lab PATCH và đổi mã trên cùng ProductId gọi `CustomerLabelColorCodeSyncService` trước SaveChanges của product, cập nhật code trong cùng lần lưu. Không thay detail, template/logo, khách hàng, sản phẩm hoặc timestamp chỉnh nội dung nhãn của Sale; audit Product hiện có ghi việc đổi mã.
- GET list/detail ưu tiên mã hiện hành của Product, fallback mã nhãn đã lưu khi sản phẩm chưa có mã. Điều này cũng xử lý đọc bản ghi cũ hoặc client tạo nhãn trùng thời điểm Lab đổi mã mà không cần backfill.
- Không thêm bảng/cột. `ColorCode` và `CustomerExternalId` trong POST vẫn được nhận để tương thích client cũ, nhưng BE lấy nguồn từ Product/Customer. Không có luồng chuyển nhãn sang sản phẩm khác.

Kiểm tra tích hợp: `CustomerLabelWorkflowPostgresTests` gọi handler Sale tạo/sửa nhãn, Lab PATCH tạo mã và luồng đổi ColorCode, rồi xóa tracking và đọc lại cả header lưu thực tế lẫn GET DTO. Test kiểm tra nội dung nhiều dòng, detail inactive, ID và timestamp Sale được giữ nguyên, cùng rollback đồng thời mã Product/nhãn. Chạy trên cluster PostgreSQL test loopback riêng có guard data_directory/database/user; chỉ transport thông báo được mock, không dùng cấu hình API/DB thật.

## Endpoints

- `GET /api/v1/plm/customer-labels?productId={guid}&customerId={guid}&isActive=true|false`
- `GET /api/v1/plm/customer-labels/{customerLabelHeaderId}`
- `POST /api/v1/plm/customer-labels`
- `PATCH /api/v1/plm/customer-labels/{customerLabelHeaderId}`
- `POST /api/v1/plm/customer-labels/{customerLabelHeaderId}/details`
- `PATCH /api/v1/plm/customer-labels/{customerLabelHeaderId}/details/{customerLabelDetailId}`

`GET` response luôn chứa `details`, kể cả những row có `isActive: false`. FE là nơi quyết định
hiển thị hoặc xử lý các row inactive; `isActive` của header chỉ lọc được qua query `isActive`.

```json
{
  "id": "header-guid",
  "productId": "product-guid",
  "customerId": "customer-guid",
  "labelType": "Bag",
  "isActive": true,
  "details": [
    { "id": "detail-guid", "lineNo": 1, "fieldKey": "material", "fieldValue": "PP", "isActive": false }
  ]
}
```

`POST` header nhận `productId`, `customerId`, các text tùy chọn và `details`. `fieldKey` phải
không rỗng và unique trong một header. `PATCH` chỉ thay đổi field được gửi. Để xóa text nullable
trên header, gửi `clearFields` với `colorCode`, `customerExternalId` hoặc `labelType`; không gửi
field nghĩa là giữ nguyên. Khi PATCH detail, dùng `clearFieldValue: true` để xóa `fieldValue`.
