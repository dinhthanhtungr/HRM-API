# Báo giá CRM

## Mục đích

Feature hỗ trợ sale tạo báo giá nháp, xin lại giá qua quy trình trao đổi bên ngoài, cập nhật giá mới
trên báo giá và ghi nhận thời điểm đã gửi báo giá cho khách hàng.

## Phạm vi

- Backend lưu báo giá, dòng sản phẩm, tổng tiền và lịch sử chuyển trạng thái.
- `refresh-prices` ghi nhận giá mới mà sale đã nhận được; endpoint này không tự gửi email xin giá.
- Luồng hiện tại không có submit, approve, accept, reject hoặc revise.
- Không tạo migration mới; feature dùng các bảng `Customer.Quotations`, `Customer.QuotationLines`
  và `Customer.QuotationStatusHistories` đã có.

## Luồng nghiệp vụ

1. Sale tạo báo giá ở trạng thái `Draft`. Nếu không truyền `externalId`, backend sinh mã prefix `BG` theo tháng.
2. Khi thay đổi danh sách sản phẩm, FE gọi `PUT /lines`; backend thay toàn bộ dòng và tính lại tổng tiền.
3. Sau khi xin lại giá, FE gọi `POST /refresh-prices` với `quotationLineId` và `unitPrice` mới.
4. Khi đã gửi khách hàng, FE gọi `POST /mark-sent`; backend chuyển `Draft` sang `Sent`, ghi `SentDate`
   và thêm một dòng lịch sử trạng thái.

Chỉ báo giá `Draft` được PATCH, thay dòng hoặc cập nhật giá. `mark-sent` gọi lại với báo giá đã `Sent`
được xử lý idempotent và không tạo thêm lịch sử.

## API

```http
POST  /api/v1/crm/quotations
PATCH /api/v1/crm/quotations/{quotationId}
PUT   /api/v1/crm/quotations/{quotationId}/lines
POST  /api/v1/crm/quotations/{quotationId}/refresh-prices
POST  /api/v1/crm/quotations/{quotationId}/mark-sent
GET   /api/v1/crm/quotations
GET   /api/v1/crm/quotations/{quotationId}
```

`POST` nhận thông tin customer/contact, currency, tỷ giá, ngày hiệu lực, điều khoản và có thể nhận danh sách
`lines` ban đầu. `PATCH` chỉ sửa phần header, không cho FE sửa `CompanyId`, sale, status, version hoặc audit field.

`PUT /lines` nhận `{ "lines": [...] }`. Mỗi dòng cần `productId`, `quantity`, `unitPrice`; `unit` có thể
bỏ trống để lấy từ Product. Backend tạo lại snapshot mã/tên sản phẩm và cấp `quotationLineId` mới cho toàn bộ dòng.

`POST /refresh-prices` nhận:

```json
{
  "lines": [
    { "quotationLineId": "guid", "unitPrice": 125000 }
  ]
}
```

Hai endpoint thay dòng/cập nhật giá trả `subTotal`, `discountAmount`, `taxAmount`, `totalAmount` sau khi tính lại.
Chi tiết báo giá trả cả lines và status histories. Danh sách hỗ trợ pagination, `keyword`, `customerId`,
`saleEmployeeId`, `status`, `from`, `to`, `sortBy` và `sortDirection`.

## Quy tắc tính tiền

- Gross dòng = `quantity * unitPrice`.
- Discount dòng = gross * `discountPercent / 100`.
- Tax dòng = (gross - discount) * `taxPercent / 100`.
- `lineTotal` = gross - discount + tax.
- Các tổng được làm tròn 6 chữ số thập phân, phù hợp precision của schema hiện tại.

`quantity` phải lớn hơn 0; `unitPrice` không âm; discount/tax nằm trong 0-100; tỷ giá phải lớn hơn 0.
Mỗi báo giá và mỗi lần cập nhật giá nhận tối đa 500 dòng.

## Phân quyền và bảo mật

- Controller yêu cầu `[Authorize]`.
- `CompanyId`, `SaleEmployeeId`, `CreatedBy`, `UpdatedBy` lấy từ current user, không nhận từ FE.
- Customer phải active, cùng company và nằm trong `CustomerVisibilityService` scope.
- Mọi GET/PATCH/PUT/action đều lọc báo giá qua customer visibility và company trước khi lọc `quotationId`
  để chống IDOR.
- Contact phải active và thuộc đúng customer. Product phải active và cùng company.
- DTO chỉ cho phép các field public cần thiết, không trả entity EF trực tiếp.

## Side effect

- Tạo/cập nhật/thay dòng/cập nhật giá ghi database và audit employee/date trên `Quotation`.
- `mark-sent` ghi `QuotationStatusHistory`; hiện không gửi email hoặc notification tự động.

## Kiểm thử quan trọng

- User không nhìn thấy customer thì không xem hoặc sửa được báo giá dù biết ID.
- Contact khác customer và product khác company phải bị từ chối.
- Chỉ Draft được sửa giá/dòng/header; báo giá rỗng không được mark sent.
- PUT lines và refresh prices phải tính đúng discount, tax và total.
- Line ID không thuộc báo giá phải bị từ chối, không cập nhật một phần.
- Gọi mark-sent lần hai không tạo thêm history.

## Giới hạn hiện tại

- Việc gửi email xin giá và gửi file báo giá cho khách hàng vẫn nằm ngoài feature này.
- Chưa có endpoint hủy/khôi phục báo giá hoặc tạo phiên bản revise.
- PATCH chưa phân biệt được JSON property không gửi với chủ động gửi `null` cho nullable date; chuỗi trắng được
  dùng để xóa các field text nullable.
