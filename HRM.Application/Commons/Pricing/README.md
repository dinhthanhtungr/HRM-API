# Giá vốn BTP nội bộ

`InternalMaterialCostingRules` là nguồn cấu hình tập trung cho giá vốn realtime của BTP nội bộ. Các hằng số chỉ
được thay đổi sau khi có quyết định nghiệp vụ; không sao chép keyword hoặc phụ phí vào Formula/Quotation handler.

| Trường hợp | Nhận diện | Công thức |
|---|---|---|
| Bột nhựa nghiền | Tên NVL có `NGHIỀN` | Giá NVL có cùng tên sau khi bỏ `NGHIỀN` + 5.000 đ/kg |
| Bột màu pha loãng | Tên NVL có `PHA LOÃNG` | Giá NVL có cùng tên sau khi bỏ `PHA LOÃNG` và nồng độ `%` x 70% |
| Hạt màu | Product category `CMB`, sau khi không khớp luật tên | Giá Formula realtime + 15.000 đ/kg khi dùng làm NVL |
| Compound | Product category `CMP`, sau khi không khớp luật tên | Giá Formula realtime + 10.000 đ/kg khi dùng làm NVL |

So khớp tên NVL không phân biệt hoa/thường và dấu tiếng Việt. Chỉ khi tìm được đúng một NVL gốc active trong cùng
công ty và NVL gốc có giá hợp lệ thì giá BTP mới hợp lệ. Không có hoặc trùng nhiều NVL gốc trả nguồn giá `Unknown`,
không fallback về giá mua trực tiếp của BTP hoặc giá bán Product.

`CMB`/`CMP` không được dùng `ProductPricingVersion` hay `MerchandiseOrder` làm giá nguyên liệu. Formula của chúng
phải tính đủ giá; nếu thiếu Formula/giá thành phần, công thức cha được báo thiếu giá. Formula/Product khác giữ nguyên
thứ tự nguồn giá cũ.

Thứ tự nhận diện luôn là **tên trước, category sau**. Vì vậy Product category `CMP` có tên chứa `NGHIỀN` áp
`giá Formula + 5.000 đ/kg`; Product có tên `PHA LOÃNG` áp `giá Formula x 70%`. Chỉ Product không khớp luật tên mới
áp phụ phí `CMB`/`CMP`.

Các giá là realtime: thay đổi giá NVL gốc được phản ánh vào Formula và giá bán đề xuất trong lần truy vấn kế tiếp.
Không ghi đè snapshot Formula, báo giá đã gửi, đơn hàng hay giá bán đã duyệt.

## Phân rã giá cho FE

Các API Formula detail, Manufacturing Formula, Formula item lookup và quotation pricing trả thêm
`price.calculation` hoặc `priceCalculation` khi user có quyền xem giá. Dữ liệu có `ruleCode`, `displayText`, giá
gốc (`baseUnitPrice`/`formulaMaterialCost`), nguồn giá gốc (`basePriceSource`), mức cộng (`fixedCostPerKg`) hoặc
tỷ lệ (`rate`) và `calculatedUnitPrice`. FE chỉ hiển thị dữ liệu server trả về; không tự tính lại. Giá trực tiếp có
`ruleCode = DIRECT_PRICE`; BTP không xác định được mã gốc duy nhất có `UNRESOLVED_INTERNAL_RULE` và `isComplete = false`.
