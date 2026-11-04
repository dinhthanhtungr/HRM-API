# Chuyển Manufacturing FE sang contract BE mới

BE mới có các command nhận ID, lựa chọn, lượng nhập, ngày kế hoạch và ghi chú.
FE không gửi CompanyId, EmployeeId, status, giá/cost, category, snapshot tên/mã,
khối lượng gốc/sau chỉnh hoặc danh sách reservation đã tính. FE cũ chưa được sửa
trong repo này; việc có endpoint mới không tự chuyển client sang endpoint đó.

## Mapping màn hình

| Màn hình/thao tác cũ | API mới |
| --- | --- |
| Tạo MFG nội bộ, FE gợi ý giá VU | POST `/api/v1/plm/production-orders/internal-basic` |
| Tạo MFG từ dòng đơn bán | POST `/api/v1/plm/production-orders/inform-basic` |
| RW chọn nguồn và xem hàng tồn | GET `/api/v1/plm/production-orders/{id}/workspace`; GET `{id}/formula-sources/{sourceId}` |
| RW dùng hàng tồn | POST `{id}/use-stock` |
| RW lưu và tạo VA | POST `{id}/formula` |
| Inform đọc/lưu | GET `{id}/inform`; PATCH `{id}/inform` |
| Copy qua payload inform/create | POST `{id}/copy` |
| Form information Save/Submit/Request/Stock | POST `{id}/actions/SavePlan`, `/Schedule`, `/RequestFormula`, `/UseStock` |
| Lab lấy nguồn tạo VA | GET `/api/v1/plm/manufacturing-formulas/production-orders/{id}/prefill` |
| Lab tạo VA | POST `/api/v1/plm/manufacturing-formulas/production-orders/{id}` |
| Lab xem/sửa VA | GET `/api/v1/plm/manufacturing-formulas/production-orders/{id}/{formulaId}/view`; POST `/api/v1/plm/production-orders/{id}/formulas/{formulaId}/process` |
| Danh sách chọn VA | GET `/api/v1/plm/manufacturing-formulas/choices` |
| Danh sách VA/VU, so sánh | GET `/api/v1/plm/manufacturing-formulas`; GET `/compare` |
| Vật tư/giá đã lưu/tồn/lot VA | GET `/api/v1/plm/manufacturing-formulas/{formulaId}/source` |
| Version lịch sử | GET `/api/v1/plm/manufacturing-formulas/{formulaId}/versions` |
| In lệnh sản xuất | POST `/api/v1/plm/manufacturing-formulas/production-orders/{id}/pdf` |
| Excel tỷ lệ, Excel lượng | GET `/api/v1/plm/manufacturing-formulas/production-orders/{id}/excel`; GET `/quantity-excel?formulaExternalId=...` |
| Chỉnh màu header/mẻ/range/PDF/preview | `/api/v1/plm/manufacturing-adjustments`, chi tiết trong SERVER-WORKFLOW |
| Color-chip CRUD/in PDF | `/api/v1/plm/manufacturing-color-chips` |
| Lịch sử máy và kiểm tra có lịch sử | GET `/api/v1/plm/production-orders/{id}/formula-sources/{sourceId}/operation-history` |

Các đường dẫn `{id}/...` rút gọn ở nhóm MFG dùng base
`/api/v1/plm/production-orders`. Query/body cụ thể nằm trong
[SERVER-WORKFLOW](SERVER-WORKFLOW.md) và DTO public của từng command.

Chỉnh màu: GET collection hỗ trợ orderId/formulaId/status/batchNo/trialNo/batchStatus/
isAdditionalBatch/includeInactive và page/pageSize; không cần gọi list theo từng MFG.
`takenQuantityGram` là tùy chọn, không tham gia tính lượng; khoảng mẻ được phép đi qua
mẻ chính và mẻ bổ sung khi đáp ứng rule trial. PDF preview có thể gửi
`previewCreatedDate` trên header/range để giữ ngày form; không thay audit khi lưu.
Xem [contract chỉnh màu](Services/Adjustments/README.md) cho semantics filter và quyền.

## Phần FE bỏ khi chuyển

- Bỏ tự chọn nhánh trạng thái New/Requested/Scheduling/Success và tự tính giá.
- Bỏ tự sinh ghi chú AUTO_PLPU_NOTE và tự ghép ghi chú Lab từ nguồn.
- Bỏ tự tính base/delta/final/tổng mẻ khi lưu chỉnh màu; chỉ gửi lượng gram nhập.
- Bỏ lời gọi `warehouse/snapshot-reserve` sau Save/Submit: action BE đồng bộ giữ
  chỗ trong transaction nếu lượng thay đổi. Đây là giữ chỗ ảo, không xuất kho.
- Bỏ gửi các giá trị OnHand/Available đã tính để quyết định lưu. Tồn hiển thị do
  BE trả; API không cam kết đủ tồn vật lý ở các workflow legacy này.
- Bỏ gửi người tạo, CreatedDate và tên/mã từ dropdown để ghi snapshot. BE lấy
  audit và dữ liệu hiện hành từ các reference đã xác minh.

Khi sửa công thức, mỗi item chỉ gồm `sourceItemId?`, `itemId`, `itemType`,
`quantity`, `lotNo?`. Dòng mới bỏ sourceItemId; xóa dòng bằng cách bỏ khỏi danh
sách thay thế. Lab cho phép dòng 0 nhưng tổng đúng 1. Các mutation thông thường
yêu cầu lượng từng dòng >0 và tổng tỷ lệ trong tolerance đã định nghĩa.

Field nullable có JsonRequired phải xuất hiện trong JSON kể cả null. Quy tắc
null/[] khác nhau theo contract: đặc biệt adjustment Ranges=null giữ mẻ,
Ranges=[] thay bằng tập rỗng; Color-chip PATCH null xóa các field nullable.

## Quyền và tương thích

Theo xác nhận chủ hệ thống, không cấp quyền xem vật tư qua fallback PLPUUser.
Người cần dùng màn hình thành phần công thức phải có claim
`plm.formula-material.view` riêng cùng các capability chức năng cần thiết. Claim
này không cấp `plm.formula-price.view` hay quyền xem cost. Token có explicit
permission set phải mang đầy đủ các claim được cấp, sau login/refresh. Không
tự chạy baseline seed hoặc cấp claim vào DB trong thay đổi này.

Giá bị che trả null, FE không chuyển null thành "giá bằng 0". Compare không
query/trả giá. PriceDate chỉ có khi người đọc được xem giá và có giá hiện hành.

Copy giữ CustomerId=null, lượng yêu cầu/giá thỏa thuận=0 theo xác nhận. RW
use-stock/save-create đổi CreatedDate=now; action UseStock của form information
giữ CreatedDate vì service information cũ không thay ngày này.

API cũ `/internal`, `/inform` và PATCH `/information` vẫn giữ contract tương
thích. Chỉ client dùng các command mới mới có đầu vào cơ bản; chưa thể nói
toàn bộ hệ thống đã chuyển chỉ dựa vào việc build BE thành công.

## Điều kiện trước khi thay hệ cũ

Kiểm thử hiện tại dùng fake persistence cho workflow và PostgreSQL provider
để kiểm tra dịch LINQ; không chứng minh rollback/concurrency trên DB thật.
Cần kiểm thử tích hợp trên DB thử nghiệm có schema legacy, xác minh hiệu lực
PSV/PSF, snapshot, reservation, số mẻ/trial, outbox và retry đồng thời. Chưa
được phép tạo schema hoặc ghi DB để thực hiện bước đó trong task hiện tại.
PDF đã chạy renderer native, chưa đối chiếu layout bằng ảnh với mẫu legacy.

Chuyển FE theo từng màn hình, kiểm tra quyền và response rồi mới bỏ lời gọi
cũ của màn hình đó. Khi cần quay lại, client dùng lại contract cũ; các dữ liệu
đã ghi vẫn nằm trong schema Manufacturing hiện hữu, không có migration mới.


## Client đã nối

`F:/UserInterface/hrm-mobile/src/modules/plm/production-orders` đã nối tạo
nội bộ/từ đơn bán và sửa information sang các contract cơ bản. VA được đọc từ
`/manufacturing-formulas/{id}/source` để giữ SourceItemId khi sửa công thức.
Giới hạn rollout Developer và capability hiện tại được giữ nguyên. Các màn
hình Lab/RW/adjustment/color-chip chưa được bổ sung vào module FE này.
