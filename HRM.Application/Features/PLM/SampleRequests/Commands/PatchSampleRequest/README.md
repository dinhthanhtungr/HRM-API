# PATCH yêu cầu phối mẫu

`PATCH /api/v1/plm/sample-requests/{sampleRequestId}` là direct patch cho màn hình chi tiết Sample Request.

Để tránh FE gửi payload thiếu field làm mất dữ liệu, direct patch chỉ cập nhật:

- string khi field được gửi khác `null`; gửi chuỗi rỗng `""` được hiểu là xóa về `null`;
- number/date/bool nullable khi field có giá trị;
- `Guid` khi field có giá trị hợp lệ và khác `Guid.Empty`.

Không dùng `null` trong direct patch để xóa number/date/bool nullable, vì model binder không phân biệt được field bị bỏ qua và field được gửi `null`. Luồng duyệt yêu cầu thay đổi dữ liệu vẫn có nhánh riêng `IsDataChangeApproval` để áp dụng các field được catalog chỉ định.

Riêng Sale/Leader không được PATCH trực tiếp ba field tiêu chuẩn kỹ thuật sau, kể cả khi muốn
xóa bằng `clearFields`: `product.food_safety`, `product.rohs_standard`, `product.reach_standard`.
FE phải gửi chúng qua `POST .../data-change-requests`; chỉ khi Lab duyệt thì backend mới patch
Product. Người thuộc `ApplicationRoleSets.PLM.ProductTechnicalEditors` vẫn có thể cập nhật trực tiếp.

Muốn xóa field rõ ràng, FE gửi `clearFields` với fieldCode nằm trong whitelist của handler:

```json
{
  "colourName": "Black",
  "clearFields": ["product.weight", "sample_request.expected_price"]
}
```

Rule:

- field không gửi: không đổi;
- field gửi value: cập nhật;
- field nằm trong `clearFields`: xóa về `null`;
- không được vừa gửi value vừa đưa cùng field vào `clearFields`;
- fieldCode không nằm trong whitelist sẽ bị từ chối.

Các field DB không nullable như `sample_request.package`, `sample_request.bag_weight`, `product.is_recycle` và `product.category_id` không được clear bằng contract này.
