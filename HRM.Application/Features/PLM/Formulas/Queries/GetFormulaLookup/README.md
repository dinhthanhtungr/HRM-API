# Lookup công thức

```http
GET /api/v1/plm/formulas/lookup
```

API này trả danh sách nhẹ để FE chọn công thức. Dữ liệu được lấy từ cả hai nơi lưu công thức:

- `sourceType = FromVU`: công thức phát triển trong bảng `Formula`.
- `sourceType = FromVA`: công thức sản xuất trong bảng `ManufacturingFormula`.
- `sourceType = FromBom`: công thức sản xuất được sinh tường minh từ M-BOM Released và có `SourceBomVersionId`.

Query hỗ trợ:

- `productId`: chỉ lấy công thức liên quan đến product.
- `sampleRequestId`: backend resolve `ProductId` của sample request trong cùng công ty hiện tại rồi lọc như `productId`.
- `sourceType`: `FromVU`, `FromVA`, `FromBom` hoặc `Both`; không gửi thì lấy tất cả. `FromVA` chỉ lấy công thức không có nguồn BOM, còn `Both` gồm cả VA legacy và công thức từ BOM.
- `statuses`: lọc nhiều trạng thái công thức theo quan hệ OR. Gửi query lặp, ví dụ `statuses=Draft&statuses=Approved`.
- `status`: tương thích ngược cho client cũ khi chỉ lọc một trạng thái; nếu gửi cùng `statuses` thì backend gộp cả hai rồi loại trùng.
- `keyword`, `pageNumber`, `pageSize`: tìm kiếm và phân trang theo contract `PaginationQuery`.
  Keyword hỗ trợ mã/tên Formula, tên/mã màu Product và mã Sample Request active liên quan.

Khi gửi `productId` hoặc `sampleRequestId`, response trả `colorCode` trực tiếp từ Product đã được scope.
Không gửi cả hai thì lookup toàn cục vẫn hoạt động. Công thức `FromBom` lấy `colorCode` trực tiếp từ Product của
BomDefinition nguồn; Formula VA legacy tiếp tục resolve từ liên kết nguồn/chuẩn/sản xuất hiện có.

Lookup luôn giới hạn trong công ty của token. Công thức VU được xác định company qua Product;
mọi ManufacturingFormula phải có `CompanyId` khớp token. Khi lọc Product, công thức `FromBom` dùng Product của
BomDefinition nguồn, còn VA legacy dùng các liên kết VU/standard/production hiện có.

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
`FromBom` cũng dùng endpoint Manufacturing Formula này; `SourceBomVersionId` chỉ mô tả nguồn tạo.
Giá chỉ xuất hiện với role có quyền xem giá Formula; khi không có quyền, các trường giá không chứa giá trị.
