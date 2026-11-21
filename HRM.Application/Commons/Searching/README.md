# Keyword search

`WhereKeyword` là helper dùng chung cho `IQueryable<T>`: keyword null/rỗng/trắng giữ nguyên query;
keyword có giá trị được trim trước khi gọi factory tạo `Expression<Func<T, bool>>`.
Helper chỉ gắn expression bằng `Where`, không Compile/Invoke, không enumerate hoặc gọi DB.

```csharp
var filtered = scopedQuery.WhereKeyword(request.Keyword, term =>
    row => EF.Functions.ILike(row.Name, term.ContainsPattern, PostgresSearchPattern.EscapeCharacter));
```

`KeywordSearchTerm.Value` là keyword đã trim. `ContainsPattern` escape `%`, `_` và `\`
để tìm literal bằng ILIKE. `StartsWith` kiểm tra tiền tố không phân biệt hoa/thường;
`StartsWithAsciiLetters(count)` kiểm tra các chữ cái đầu A-Z/a-z. Hai hàm chọn nhánh
phải gọi trong factory trước khi trả expression, không đặt trong expression gửi xuống DB.
Nếu cần exact/prefix pattern, dùng `PostgresSearchPattern.ExactLiteral/PrefixLiteral(term.Value)`.

Mỗi feature chịu trách nhiệm chọn field, thứ tự nhánh, active, company và quyền.
Áp dụng lên query đã scope, trước Count/paging; không dùng helper thay authorization.
Không đặt tiền tố nghiệp vụ như DHG/TP_ vào Commons. Ví dụ dùng thực tế:
`Features/Dispatch/DeliveryOrders/Queries/GetSelectableDeliveryLines/SelectableDeliveryKeywordSearch.cs`.

## Smart search trên trang danh sách

Phần dùng chung chỉ dựng biểu thức tìm kiếm, kiểm tra badge và chia gợi ý. Feature tự khai báo trường/nhãn và truyền `IQueryable` đã giới hạn công ty, quyền, trạng thái và bộ lọc. Không tự áp dụng lên lookup hoặc endpoint khác chỉ vì request kế thừa `PaginationQuery`.

## Tổ chức và contract

Parser chấp nhận cả alias do feature khai báo và `field.Key:` chuẩn, chỉ với key có trong schema hiện tại. FE chuẩn hóa nhãn Việt/Anh (ví dụ `Mã khách hàng:`) thành `customerCode:` trước khi gửi gợi ý/danh sách; không thêm trường hoặc quyền khi nhận key. Summary có parser riêng nhưng nhận cùng các key tương ứng; phần tương đương NVL kiểm tra metadata sau nghiệp vụ cũng nhận key chuẩn. Client cũ gửi alias vẫn tương thích. Tham chiếu FE: `src/modules/search/PREFIX_AUDIT.md`.

- `SmartSearchRequest.cs`: hai query param tùy chọn `searchSelections`, `suggestionKeyword` và DTO gợi ý `{field,value,matchRank}`.
- `SmartSearchSelections.cs`: JSON whitelist, tối đa 20 badge, 6.000 ký tự; dữ liệu sai không mở rộng truy vấn.
- `SmartSearchField.cs`, `SmartSearch.cs`: khai báo trường scalar/collection bằng expression, không compile hoặc lọc EF bằng bộ nhớ.
- `SmartSearchSchema.cs`: badge cùng field là OR, khác field là AND; prefix nhãn; truy vấn gợi ý một lượt UNION ALL, tối đa 10 giá trị mỗi nhánh trước khi chia tối đa 10 kết quả cuối. Loại ít kết quả nhường chỗ cho loại còn lại.
- `SearchNameText.cs`: dùng `(?:.|\n)` để tìm xuyên dòng, không ghép cờ inline `(?s)` với cờ do Npgsql sinh; tên không phân biệt dấu và thứ tự các từ; mã tìm chuỗi literal, không biến `%`/`_` thành wildcard người dùng.

Request bình thường giữ response cũ. Request có `suggestionKeyword` chỉ trả metadata trong `PagedResult.searchSuggestions`, không chạy count, phân trang chi tiết hoặc tính giá nếu feature có đường đi nhanh tương ứng. BOM và PLPU có endpoint `/suggestions` riêng trả array vì response danh sách hiện tại khác cấu trúc. `searchSuggestions` bị bỏ khỏi JSON nếu không được dùng.

Khi gợi ý, bỏ badge của chính trường đang gợi ý để có thể chọn badge thứ hai theo OR; giữ badge của các trường khác. Nội dung đang gõ thay cho keyword cũ, các bộ lọc nghiệp vụ vẫn giữ. Nhãn được nhận diện trước dấu `:`/`：`; nhãn không nhận diện giữ hành vi tìm tự do. Nhãn chưa có nội dung không tự tìm mọi bản ghi.

## Nơi cấu hình nghiệp vụ

MST khách hàng dùng canonical field `customerTaxNumber`, nhãn `MST KH` và alias `MST:`, `Mã số thuế:`, `Tax number:`. Khai báo alias dùng chung ở `Features/CRM/CustomerCare/Services/Search/CustomerTaxSearch.cs`; feature cung cấp selector đã scope theo company/quyền. Tìm mã literal giữ số 0 đầu và dấu gạch nối, đọc `Customer.TaxNumber` hiện tại. Áp dụng cho các nguồn có tìm khách hàng, không mở rộng lookup/NCC. MyTasks chỉ tra MST trong khách hàng có quyền CRM. Sample Request Summary có loại gợi ý riêng và mảng `customerTaxNumberIds` (tối đa 20 ID, không nhận Guid.Empty); badge MST và tên/mã KH là các nhóm điều kiện AND độc lập. Giữ tối đa 10 gợi ý.

Trường và alias nằm cạnh handler hoặc helper riêng của feature, ví dụ `Features/PLM/SaleOrders/Services/SaleOrderSmartSearch.cs`, `Features/PLM/Boms/Queries/GetBoms/BomSearchQueryRules.cs`, `Features/Warehouse/Queries/GetStockMovements/StockMovementSmartSearch.cs`.

Các trang đang nối: yêu cầu mẫu Summary; công việc Sale/Lab/theo dõi mẫu và báo cáo chạy thử; lệnh sản xuất mẫu, lệnh sản xuất, ColorChip; timeline đơn bán; E-BOM/M-BOM, quy trình, hướng dẫn thao tác, hồ sơ hao hụt; khách hàng, activity khách hàng, việc cá nhân; báo giá, bảng giá sản phẩm/lựa chọn giá/chính sách giá, tổng quan giá; NCC, NVL, đánh giá giá NVL, đơn mua; tồn kho, biến động, phiếu kho; IQC nhận hàng, kiểm sản phẩm, COA, tiêu chuẩn; nhân viên, thiết bị; NVL tương đương và PLPU.

Ngoại lệ có chủ đích: lookup/form/dialog không đổi; trang placeholder, bài thực hành dữ liệu giả và trang không có ô keyword không thêm input. Activity board tìm mã/tên khách hàng theo đúng panel khách hàng; việc cá nhân có thêm tên công việc/nhân viên. Chế độ nhu cầu NVL và phần tương đương nhúng trong lookup giữ hành vi cũ. Phiên bản giá dùng chung bộ lọc với trang lựa chọn giá, không thêm một ô tìm mới.

IQC dùng metadata từ cả phiếu kho và lab test; điều kiện nguồn vẫn giữ nguyên. Biến động kho lọc cả nhóm để giữ đủ các vế. Tương đương NVL lọc SQL trước rồi kiểm tra lại metadata sau khi nghiệp vụ đã chọn công thức/NVL thực tế; không gợi ý NVL từ công thức không được chọn. Giá bán cho người không quản lý giá chỉ gợi ý mã công thức từ phiên bản đã duyệt trong đúng công ty/tiền tệ. Đánh giá giá NVL không gợi ý NCC khi thiếu quyền chi tiết. Export tồn kho truyền cùng `searchSelections`, không truyền chế độ gợi ý.

## Bổ sung trường hoặc trang

1. Khai báo field/alias tại feature trên query đã scope; rà quyền xem trường liên quan.
2. Áp dụng badge/prefix trước count/page và gợi ý trước phần nạp chi tiết; giữ các bộ lọc khác.
3. Thêm source/field whitelist, endpoint và nhãn vi/en ở module FE `src/modules/search`.
4. Nối hook query, UI và các luồng export/copy-query liên quan. Không lấy một page dữ liệu để giả làm toàn bộ gợi ý.
5. Kiểm tra dịch SQL, tenant/permission, OR/AND, quota, request cũ bị hủy và Enter/IME. Query dùng chung phải rà caller.

## Hiệu năng và giới hạn

Daily-work “Tất cả mẫu” có luồng gợi ý metadata riêng, mô tả tại [Sample Work smart search](../../Features/PLM/SampleRequests/Queries/Reporting/GetSampleRequestDailyWork/SMART_SEARCH.md); các bucket phân loại công việc vẫn giữ query nghiệp vụ.

Frontend bắt đầu request ở frame kế tiếp, hủy request cũ và bỏ response cũ/khác phiên công ty; không dùng cache dùng chung giữa user. Mỗi field chỉ đọc mã/tên, distinct và limit tại SQL. Những feature cần xác định trạng thái giá hoặc tính tồn trước khi biết bản ghi hợp lệ phải giữ bước đó; không bỏ kiểm tra để chạy nhanh hơn.

Không cam kết mọi request dưới 50 ms từ build/unit test. Thời gian thực tế còn phụ thuộc dữ liệu, query plan, số field, bộ lọc và mạng. Cần đo trên tiến trình đang phục vụ đúng repo D; không tự tạo index, migration hoặc đổi DB thật để đạt mục tiêu.
