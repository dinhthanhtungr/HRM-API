# PLM Formula Rules

Đọc khi sửa Formula/ManufacturingFormula/material policy, resolve formula, hiển thị item, lookup hoặc keyword theo Product.ColourCode/SampleRequest.ExternalId/Formula.ExternalId trong PLM, kể cả query Sample Request. Flow tạo/sửa Sample Request hoặc gửi message đọc thêm `plm-sample-requests.md`.

## Formula

- Formula/material API phải tôn trọng policy hiện có nếu data nhạy cảm về material/giá.
- Field giá và material có thể cần policy riêng như formula price/material viewers.
- Khi resolve formula theo product/sample request, phải check active và company scope nếu entity có `CompanyId`.
- Lookup/form-options nên rõ contract và không phình DTO của endpoint khác nếu mục đích khác nhau.
- Tôn trọng rule trong PLMCustomerRules.cs để nhận biết việc tính toán đối với khách hàng đặc biệt (khách nội bộ); luật này dùng chung cho nhiều nơi phải đặt tại đây.
- Mọi API GET đang hiển thị item của Formula/ManufacturingFormula theo dữ liệu hiện tại phải dùng `FormulaItemDisplayResolver`, không tự ghép `MaterialNameSnapshot`/`MaterialExternalIdSnapshot` hoặc tự query Product/SampleRequest từng nơi. Resolver tải theo batch, Material hiển thị tên/mã hiện tại; Product hiển thị `[ColourCode] Name` và `ExternalId` của Sample Request active mới nhất. Snapshot chỉ là fallback khi mất quan hệ nguồn. Không áp dụng rule này cho FormulaVersion, `FormulaMaterialSnapshots`, export/PDF hoặc chứng từ lịch sử vì các endpoint đó phải giữ snapshot tại thời điểm nghiệp vụ.
- Với API keyword đang tìm bằng `Product.ColourCode`, phải đồng thời hỗ trợ mã `SampleRequest.ExternalId` active cùng company và `Formula.ExternalId` liên quan trong cùng scope. Dùng `Any`/`EXISTS` hoặc điều kiện navigation được EF translate; không join làm nhân bản dòng và không bỏ company/visibility scope. Nếu endpoint vốn là Sample Request thì mã Sample Request của chính bản ghi đã có sẵn, chỉ cần bổ sung Formula liên quan.
- Mọi filter EF theo `Formula.ExternalId` (kể cả navigation hoặc snapshot `FormulaExternalId`) phải dùng `EF.Functions.ILike` với pattern phù hợp (`%keyword%` hoặc `keyword%`); không dùng `Contains`/`StartsWith` vì collation có thể phân biệt hoa thường. Nếu handler đã materialize dữ liệu trước khi lọc, phải đưa filter này xuống `IQueryable` trước `ToListAsync`.

## Documentation

Nếu đổi formula/material contract, cập nhật README gần feature nếu đã có; nếu chưa có và flow đáng chú ý, tạo README gần feature.
