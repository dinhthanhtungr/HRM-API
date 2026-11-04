# CRM Quotations

Đọc file này khi task liên quan báo giá, quotation, quotation line, refresh price, mark sent, pricing snapshot hoặc dropdown công thức/giá cho sale.

## Business Rules

- Báo giá là snapshot tại thời điểm lập/gửi; không để báo giá cũ bị thay đổi giá khi product/formula/giá nguồn đổi sau này.
- Line phải snapshot product code/name/unit và unit price.
- Nếu thêm formula/pricing source cho quotation, line nên snapshot `FormulaId`, `FormulaExternalId`, `PriceDate`, `ConfirmedBy`, `ConfirmedDate` nếu contract yêu cầu.
- Refresh price là hành động rõ ràng, không âm thầm đổi giá line đã gửi nếu rule không cho phép.
- Mark sent phải cập nhật status/history theo pattern hiện có.

## Đọc Thêm Theo Phạm Vi

- Publish/recipient/topic/payload/conversation, mark-sent hoặc side effect message/notification: `quotations-notifications.md`.
- Formula/pricing lookup, options/workbench, drawer/workspace/version, tiers hoặc nguồn giá/snapshot: `quotations-pricing.md`.
- CustomerCare chịu tác động: `crm.md`. Resolve/display/search Formula hoặc Sample Request chịu tác động: `plm.md`.

## Security

- Mỗi query/command phải check company scope của quotation, customer, product và formula nếu có.
- Không cho FE set `CompanyId`, `SaleEmployeeId`, created/updated fields hoặc status đặc quyền trực tiếp.
- Validate line count, quantity, unit price, discount/tax percent, product active và cùng company.

## Documentation

Nếu đổi API/DTO/rule/status/side effect của báo giá, cập nhật `HRM.Application/Features/CRM/Quotations/README.md` theo mục liên quan và scope/quyền/invariant chung. Chi tiết response định giá nằm ở `quotations-pricing.md`.

