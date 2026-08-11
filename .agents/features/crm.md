# CRM CustomerCare

Đọc file này khi task liên quan CRM, customer, lead, claim, transfer, interaction, work plan, follow-up task hoặc AI summary.

## Scope

- CRM data user-facing phải lọc theo company/current user và visibility rule hiện có.
- Không để sale xem/sửa customer ngoài scope nếu feature có ownership/assignment/claim.
- Public DTO không trả entity DB trực tiếp.
- Lead/customer transfer phải giữ audit/trace theo pattern feature hiện có.

## Workflow Shape

Handler nên đọc theo flow:

1. Validate request.
2. Resolve current employee/company.
3. Load aggregate/data với company scope.
4. Check permission/visibility/ownership.
5. Apply business rule.
6. Save DB và publish side effect nếu có.
7. Return DTO/result rõ contract.

## AI Summary

- Không ghi API key Gemini vào source hoặc README.
- Không log payload nhạy cảm.
- Kiểm tra rate limit service và config khi thay đổi AI summary.
- Không gọi AI lại nếu feature đã có rule cache/success và request không force regenerate.

## Documentation

Nếu đổi hành vi CRM, cập nhật `HRM.Application/Features/CRM/CustomerCare/README.md` và `CHANGELOG.md` nếu thay đổi có tác động user/API/bảo mật/vận hành.

