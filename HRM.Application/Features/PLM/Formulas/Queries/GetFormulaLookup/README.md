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
- `status`: lọc trạng thái công thức.
- `keyword`, `pageNumber`, `pageSize`: tìm kiếm và phân trang theo contract `PaginationQuery`.

Response là `PagedResult<FormulaLookupDto>`:

```json
{
  "items": [
    {
      "formulaId": "00000000-0000-0000-0000-000000000000",
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
`GET /api/v1/plm/manufacturing-formulas/{formulaId}/materials` nếu cần lazy-load vật tư sản xuất.
