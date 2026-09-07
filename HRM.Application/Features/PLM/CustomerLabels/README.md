# Customer Labels

API `/api/v1/plm/customer-labels` quản lý mẫu nhãn theo cặp `CustomerId` và `ProductId`.
Mọi endpoint yêu cầu đăng nhập. API chỉ truy cập header khi cả Customer và Product đều thuộc
company hiện tại; tạo và cập nhật yêu cầu policy `PLM.ProductTechnicalInfo.Edit`.

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
