# Dashboard báo cáo giao hàng hoàn tất

## Endpoint và quyền

`GET /api/v1/dispatch/reports/finish-dashboard?from=2026-11-05&to=2026-11-05&pageNumber=1&pageSize=15`

- Authorize + handler cùng kiểm tra `SaleUser`, `Leader`, `President`, `Developer`, `ACCUser`, `PLPUUser`; không thêm role claim/capability hoặc seed DB.
- President/Developer/ACCUser/PLPUUser: toàn công ty hiện tại, bao gồm khách inactive trong lịch sử; không truy cập khác company. Role công ty ưu tiên khi tài khoản có nhiều role.
- SaleUser: khách active đang có assignment cá nhân active hoặc Work claim cá nhân chưa hết hạn, cùng company.
- Leader mặc định xem KH cá nhân và các KH có assignment active hoặc Work claim chưa hết hạn thuộc nhóm mình quản lý. Nhóm được xác định bằng MemberInGroups active, IsAdmin=true, Profile=employee và Group.CompanyId=current company. Không nhận groupId từ client. `ownOnly=true` giới hạn lại KH cá nhân; SaleUser không được mở rộng bằng `ownOnly=false`. Leader không có nhóm chỉ thấy KH cá nhân.
- `scope` response: `Company`, `TeamCustomers`, `OwnCustomers`. `ownOnly` mặc định false, role công ty luôn Company. CustomerViewAll/Admin/CurrentSaleId không mở rộng phạm vi.
- Sale thiếu employee/context bị từ chối. Các role khác nhận 403; chưa đăng nhập nhận 401.
- Không nhận companyId/saleId/customerId từ client. Cả KH trên phiếu và KH nguồn ĐHG phải thuộc scope,
  nên dữ liệu legacy gắn sai KH không thể lọt qua tổng, biểu đồ, top hoặc bảng.
- Date sai/context thiếu trả 400 OperationResult; tối đa 366 ngày tính cả hai đầu. Page size chuẩn hóa 1–100,
  page âm về 1; chặn overflow Skip. Không ghi dữ liệu, migration, notification hoặc tự restart dịch vụ.

## Nguồn và công thức

Theo lựa chọn của user, dùng cùng `DeliveryFinishReportQuery.Project` của Excel Finish:
lọc ngày tạo phiếu `CreatedDate >= from.Date && CreatedDate < to.Date + 1 ngày`.
Không tự thêm điều kiện status/HasPrinted/loại hàng kèm. Tên “hoàn tất” là tên báo cáo gốc,
không có nghĩa mọi phiếu trong kỳ đã giao xong. API Excel cũ giữ contract/quyền cũ và không được dùng
thay endpoint dashboard để tải dữ liệu hoặc export từ màn này.

- QuantityKg = tổng quantity mỗi delivery detail active đúng một lần, kể cả hàng kèm như Excel.
- Bags = tổng NumOfBags (long). Không nhân theo số Lot, người giao hay MFG.
- DeliveryCount/OrderCount/CustomerCount/ProductCount là distinct ID; Order/Product thiếu ID không tăng count.
- LineCount là số dòng nguồn sau scope, đồng thời là totalCount của bảng; summary/chart không tính từ trang FE.
- Daily có đủ ngày trong kỳ, thiếu ghi nhận trả 0. KH/SP trả đầy đủ các nhóm tổng hợp theo kg giảm dần, tie theo ID; FE hiển thị dần trong vùng cuộn. Chỉ tải các nhóm tổng hợp, không tải toàn bộ dòng phiếu.
- Nhãn KH/SP lấy snapshot trong báo cáo Excel. Một ID có nhiều nhãn lấy MAX chuỗi ổn định;
  nhóm thiếu ProductId gộp thành “Chưa xác định”, không gán tên một sản phẩm khác.
- Bảng mỗi dòng có Id = delivery detail ID ổn định; thứ tự ngày tạo, phiếu ID, detail ID.
  Không gộp các dòng cùng sản phẩm hoặc Lot. Ngày hứa giao lấy MAX ExpectedDate của MFG active như Excel,
  null khi thiếu liên kết; chỉ query theo lô các dòng thuộc trang đã qua scope.
- `summary.revenueVnd`: doanh số VND chưa VAT từ `DeliveryRevenueQuery` chung:
  quantity dòng giao × UnitPriceAgreed × tỷ giá đơn (fallback hệ số 1 theo helper).
  Loại phiếu hủy, dòng/phiếu inactive, khách nội bộ; cần dòng ĐHG. Ngày ghi nhận là ngày tạo phiếu.
  Scope cả KH phiếu và KH ĐHG, company phiếu/ĐHG trước SUM SQL; empty trả 0, không tính từ trang detail.
  Các role dashboard đều xem trong scope hiện tại. Các KPI lượng/bảng giữ nguồn Excel nên có thể khác tập doanh số.
- Không đọc/return giá vốn, đơn giá riêng, margin hoặc DeliveryPrice. Không round dữ liệu ở BE;
  FE định dạng kg tối đa 3 chữ số thập phân. Thời điểm “giao” của báo cáo gốc là ngày tạo phiếu,
  UI ghi rõ “Ngày tạo phiếu”, không tự coi là thời điểm khách nhận hàng.

Các aggregate đẩy xuống SQL; chỉ tải aggregate và một trang detail. Dùng helper scope trước GroupBy/Count/Skip/Take.
Các query trong request đọc dữ liệu hiện tại, không tạo snapshot giao dịch cố định trong lúc có ghi đồng thời.

## Response rút gọn

```json
{
  "success": true,
  "data": {
    "from": "2026-11-05T00:00:00", "to": "2026-11-05T00:00:00", "scope": "OwnCustomers",
    "summary": {"quantityKg": 12, "bags": 1, "deliveryCount": 1, "orderCount": 1, "customerCount": 1, "productCount": 1, "lineCount": 1},
    "daily": [{"date":"2026-11-05T00:00:00","quantityKg":12,"bags":1,"deliveryCount":1}],
    "customers": [{"id":"00000000-0000-0000-0000-000000000001","name":"Khách mẫu","quantityKg":12,"bags":1}],
    "products": [],
    "rows": {"items":[{"id":"00000000-0000-0000-0000-000000000002","data":{"deliveryExternalId":"PGH-MAU","quantityKg":12,"numOfBags":1,"expectedDeliveryDate":null}}],"totalCount":1,"pageNumber":1,"pageSize":15,"totalPages":1,"hasPreviousPage":false,"hasNextPage":false}
  }
}
```

Empty: summary toàn 0, daily các ngày 0, rankings/rows.items rỗng; null là dữ liệu chưa có, không suy ra 0.

## FE và verification

Route `/reports/delivery-finish`, mục Báo cáo; date dùng SmartDateFilter và giữ query from/to/page trên URL.
Repository → use case → hook; cache tách auth generation/user/company, abort khi unmount, không giữ dữ liệu user cũ.
FE kiểm tra quyền để hiển thị, BE là lớp enforce cuối. Chỉ xem, chưa có export mới để tránh dùng nhầm API Excel rộng hơn.
Test scope role/company/assignment/claim, legacy trộn KH, count/Lot/date/paging và dịch SQL; không test trên DB thật.
