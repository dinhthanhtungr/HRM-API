# Lookup công thức

```http
GET /api/v1/plm/formulas/lookup
```

API này trả danh sách nhẹ để FE chọn công thức. Dữ liệu được lấy từ cả hai nơi lưu công thức:

- `sourceType = FromVU`: công thức phát triển trong bảng `Formula`.
- `sourceType = FromVA`: công thức sản xuất trong bảng `ManufacturingFormula`.

Query hỗ trợ:

- `productId`: chỉ lấy công thức liên quan đến product.
- `sampleRequestId`: backend resolve `ProductId` của sample request trong cùng công ty hiện tại rồi lọc như `productId`.
- `sourceType`: `FromVU`, `FromVA` hoặc `Both`; không gửi thì lấy cả hai nguồn.
- `statuses`: lọc nhiều trạng thái công thức theo quan hệ OR. Gửi query lặp, ví dụ `statuses=Draft&statuses=Approved`.
- `status`: tương thích ngược cho client cũ khi chỉ lọc một trạng thái; nếu gửi cùng `statuses` thì backend gộp cả hai rồi loại trùng.
- `keyword`, `pageNumber`, `pageSize`: tìm kiếm và phân trang theo contract `PaginationQuery`.
  Keyword hỗ trợ mã/tên Formula, tên/mã màu Product và mã Sample Request active liên quan.

Khi gửi `productId` hoặc `sampleRequestId`, response trả `colorCode` trực tiếp từ Product đã được scope.
Không gửi cả hai thì lookup toàn cục vẫn hoạt động, nhưng `colorCode` trả chuỗi rỗng vì một Formula VA
có thể liên kết nhiều Product và API không được phép chọn ngẫu nhiên một Product qua lịch sử sản xuất.

Lookup luôn giới hạn trong công ty của token. Công thức VU được xác định company qua Product,
không phụ thuộc vào `Formula.CompanyId`, để vẫn tìm được dữ liệu Formula cũ chưa có company id.
Khi có `productId`, công thức VA cũng được xác định qua Product liên kết; khi không có `productId`,
VA phải có `ManufacturingFormula.CompanyId` khớp company của token.

Response là `PagedResult<FormulaLookupDto>`:

```json
{
  "items": [
    {
      "formulaId": "00000000-0000-0000-0000-000000000000",
      "colorCode": "WHITE-01",
      "sourceType": "FromVU",
      "externalId": "VU260700001",
      "name": "Formula VU",
      "status": "Draft",
      "note": "Ghi chú",
      "materialCount": 3,
      "createdDate": "2026-07-29T10:30:00"
    }
  ],
  "totalCount": 1,
  "pageNumber": 1,
  "pageSize": 15,
  "totalPages": 1,
  "hasPreviousPage": false,
  "hasNextPage": false
}
```

Lookup không trả material detail, giá realtime hoặc `pricing`. Khi FE chọn `sourceType = FromVU`, dùng
`GET /api/v1/plm/formulas/{formulaId}` để lấy chi tiết công thức VU. Khi FE chọn `sourceType = FromVA`, dùng
`GET /api/v1/plm/manufacturing-formulas/{formulaId}/materials` để lazy-load vật tư sản xuất cùng giá realtime.
Giá chỉ xuất hiện với role có quyền xem giá Formula; khi không có quyền, các trường giá không chứa giá trị.
