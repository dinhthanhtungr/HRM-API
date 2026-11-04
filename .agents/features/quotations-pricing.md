# Quotation Pricing Contract Rules

Đọc khi sửa formula/pricing lookup, product-pricing-options/workbench, pricing drawer/workspace/version, quotation tiers hoặc nguồn giá/snapshot. Dữ liệu nhạy cảm đọc capability/field visibility theo root; side effect notification đọc `quotations-notifications.md`.

## Dropdown Formula/Pricing

- Nếu sale cần chọn công thức và xem giá gần nhất, tạo endpoint lookup/options riêng cho báo giá thay vì dùng lại sample request lookup.
- Dropdown chỉ là preview; BE vẫn phải validate lại formula/product/company và lưu snapshot khi tạo/cập nhật báo giá.
- Giá/label trả về nên là contract rõ ràng; status/type/source nên là enum/code ổn định nếu FE có thể tự map.

## Documentation

Với response định giá (`product-pricing-options`, `product-pricing-workbench`, pricing drawer/workspace,
pricing version và quotation price tiers), README bắt buộc ghi rõ:

- Giá/cost nào là realtime, snapshot đã lưu, giá hiệu lực hay giá hệ thống gợi ý.
- `pricingStatus`, `isSystemCalculatedDraft`, version id và `isStored` kết hợp thế nào để biết đã có record DB hay chưa.
- `currentMaterialCost`, `storedMaterialCostSnapshot`, chênh lệch, ba giá hiệu lực và nguồn công thức được resolve ra sao.
- `selectedSource`, `draftPricing`, `approvedPricing`, `displayPriceTiers`, history và related quotations đại diện cho gì.
- Ngày cập nhật thuộc Formula/MFG Formula, ProductPricingVersion hay Quotation; không gọi chung là "ngày cập nhật giá" nếu nguồn khác nhau.
- Field nào Sale được xem, field nào chỉ President/Developer được xem hoặc sửa.

Phải có ít nhất một payload mẫu đại diện và giải thích `null`/danh sách rỗng. Không chỉ chép DTO hoặc JSON vào README.
