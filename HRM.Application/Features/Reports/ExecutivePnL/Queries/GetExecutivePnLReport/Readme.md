# Executive PnL Report

## 1. Mục đích

Feature `GetExecutivePnLReport` dùng để lấy dữ liệu báo cáo P&L theo tháng.

Report hiện tại gom dữ liệu từ nhiều nguồn:

* `MerchandiseOrderDetails`: đơn giá bán, currency/tỷ giá và thông tin customer liên kết.
* `MerchandiseOrders`: số lượng đơn hàng.
* `DeliveryOrderDetails`: doanh thu, giá vốn, số lượng đã giao và số dòng ghi nhận doanh số.
* `DeliveryOrders`: phí vận chuyển.
* `MfgProductionOrders`: sản lượng sản xuất và số lượng lệnh sản xuất.

Toàn bộ chỉ số doanh số của P&L tổng, Customer, Trend, Sales, Product Type và CRM Activity Report dùng chung
`HRM.Application.Commons.Reporting.DeliveryRevenueQuery`. Doanh số được ghi nhận theo
`DeliveryOrder.CreatedDate` và tính bằng
`DeliveryOrderDetail.Quantity * MerchandiseOrderDetail.UnitPriceAgreed`. Nguồn chỉ lấy delivery order/detail
active có liên kết Merchandise Order Detail, loại khách nội bộ `KH_VIETAUS`, loại delivery trạng thái `Canceled`
và quy đổi về VND theo currency/tỷ giá của Merchandise Order liên kết.

Các dashboard phân rã theo sale/product vẫn áp scope phân quyền và assignment riêng. Vì vậy tổng của chúng chỉ
đối soát với Customer/Trend trên cùng kỳ, company và cùng tập customer mà người dùng được phép xem/phân bổ.

Kết quả cuối cùng được trả về dạng `ExecutivePnLReportDto` để frontend render thành bảng report động theo tháng.

---

## 2. Cấu trúc thư mục đề xuất

```txt
ExecutivePnL
  Dtos
    ExecutivePnLReportDto.cs
    ExecutivePnLColumnDto.cs
    ExecutivePnLSectionDto.cs
    ExecutivePnLLineDto.cs

  Queries
    GetExecutivePnLReport
      GetExecutivePnLReportQuery.cs
      GetExecutivePnLReportQueryHandler.cs

      Models
        ExecutivePnLFilter.cs
        ExecutivePnLPeriod.cs
        ExecutivePnLMonthlyActual.cs

      Services
        ExecutivePnLActualReader.cs
        ExecutivePnLReportBuilder.cs
        ExecutivePnLCalculator.cs
```

---

## 3. Vai trò từng nhóm file

### 3.1. `Dtos`

`Dtos` chứa dữ liệu trả ra frontend.

#### `ExecutivePnLReportDto`

Đại diện cho toàn bộ report.

```csharp
public sealed class ExecutivePnLReportDto
{
    public string CompanyName { get; set; } = string.Empty;
    public string BusinessUnit { get; set; } = "MASTER BATCH";
    public string Currency { get; set; } = "VND";
    public DateTime FromMonth { get; set; }
    public DateTime ToMonth { get; set; }

    public List<ExecutivePnLColumnDto> Columns { get; set; } = new();
    public List<ExecutivePnLSectionDto> Sections { get; set; } = new();
}
```

Ý nghĩa:

* `CompanyName`: tên công ty đang xem report.
* `BusinessUnit`: nhóm kinh doanh, mặc định là `MASTER BATCH`.
* `Currency`: đơn vị tiền tệ, mặc định là `VND`.
* `FromMonth`: tháng bắt đầu.
* `ToMonth`: tháng kết thúc.
* `Columns`: danh sách cột của report.
* `Sections`: danh sách nhóm dòng trong report.

---

#### `ExecutivePnLColumnDto`

Đại diện cho một cột trong bảng report.

```csharp
public sealed class ExecutivePnLColumnDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string ColumnType { get; set; } = string.Empty;
}
```

Ví dụ:

```json
{
  "key": "actual_2026_01",
  "label": "2026-01",
  "columnType": "Actual"
}
```

Ý nghĩa:

* `Key`: mã cột, dùng để map với dữ liệu trong `Values`.
* `Label`: tên hiển thị trên giao diện.
* `ColumnType`: loại cột, ví dụ `Actual`, `ActualTotal`.

---

#### `ExecutivePnLSectionDto`

Đại diện cho một nhóm dòng trong report.

```csharp
public sealed class ExecutivePnLSectionDto
{
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public List<ExecutivePnLLineDto> Lines { get; set; } = new();
}
```

Ví dụ các section hiện tại:

```txt
SALES
COST
MARGIN
OPERATIONS
```

Ý nghĩa:

* `Code`: mã section.
* `Title`: tên hiển thị.
* `SortOrder`: thứ tự hiển thị.
* `Lines`: danh sách dòng số liệu thuộc section đó.

---

#### `ExecutivePnLLineDto`

Đại diện cho một dòng số liệu trong report.

```csharp
public sealed class ExecutivePnLLineDto
{
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsBold { get; set; }
    public bool IsSubtotal { get; set; }
    public Dictionary<string, decimal> Values { get; set; } = new();
}
```

Ví dụ:

```json
{
  "code": "TOTAL_SALES",
  "label": "Total sales",
  "sortOrder": 40,
  "isBold": true,
  "isSubtotal": true,
  "values": {
    "actual_2026_01": 10000000,
    "actual_2026_02": 15000000,
    "total_actual": 25000000
  }
}
```

Ý nghĩa:

* `Code`: mã dòng.
* `Label`: tên dòng hiển thị.
* `SortOrder`: thứ tự dòng trong section.
* `IsBold`: frontend có thể dùng để in đậm.
* `IsSubtotal`: đánh dấu dòng tổng phụ.
* `Values`: dữ liệu theo từng cột.

`Values` dùng `Dictionary<string, decimal>` vì số lượng tháng là động. Frontend sẽ lấy dữ liệu theo `Column.Key`.

Ví dụ:

```txt
Column.Key = actual_2026_01
Line.Values["actual_2026_01"] = 10000000
```

---

## 4. Models nội bộ

Các model trong folder `Models` chỉ phục vụ nội bộ cho query `GetExecutivePnLReport`.

Chúng không phải entity database và cũng không phải DTO trả ra frontend.

---

### 4.1. `ExecutivePnLFilter`

Dùng để chứa điều kiện report đã được chuẩn hóa.

```csharp
internal sealed class ExecutivePnLFilter
{
    public Guid? CompanyId { get; init; }
    public string? BusinessUnit { get; init; }
    public string? Currency { get; init; }
    public DateTime FromMonth { get; init; }
    public DateTime ToMonth { get; init; }

    public DateTime RangeEnd => ToMonth.AddMonths(1);
}
```

Vai trò:

* Gom các điều kiện lọc cần dùng.
* Tránh truyền nguyên `GetExecutivePnLReportQuery` vào mọi service nhỏ.
* Giúp các hàm nhỏ chỉ phụ thuộc vào dữ liệu cần thiết.

Ví dụ:

```csharp
var filter = new ExecutivePnLFilter
{
    CompanyId = request.CompanyId,
    BusinessUnit = request.BusinessUnit,
    Currency = request.Currency,
    FromMonth = period.FromMonth,
    ToMonth = period.ToMonth
};
```

---

### 4.2. `ExecutivePnLPeriod`

Dùng để xử lý logic tháng.

Vai trò:

* Chuẩn hóa `FromMonth` và `ToMonth` về ngày đầu tháng.
* Tự đảo tháng nếu `FromMonth > ToMonth`.
* Tạo danh sách tháng cần hiển thị.
* Tạo `MonthKey`.

Ví dụ key:

```txt
actual_2026_01
actual_2026_02
total_actual
```

Ví dụ class:

```csharp
internal sealed class ExecutivePnLPeriod
{
    public DateTime FromMonth { get; }
    public DateTime ToMonth { get; }
    public IReadOnlyList<DateTime> Months { get; }

    private ExecutivePnLPeriod(DateTime fromMonth, DateTime toMonth)
    {
        FromMonth = fromMonth;
        ToMonth = toMonth;
        Months = BuildMonths(fromMonth, toMonth);
    }

    public static ExecutivePnLPeriod Create(DateTime? fromMonth, DateTime? toMonth)
    {
        var to = FirstDayOfMonth(toMonth ?? DateTime.Today);
        var from = FirstDayOfMonth(fromMonth ?? to.AddMonths(-5));

        if (from > to)
        {
            (from, to) = (to, from);
        }

        return new ExecutivePnLPeriod(from, to);
    }

    public static string MonthKey(int year, int month)
    {
        return $"actual_{year:D4}_{month:D2}";
    }

    private static List<DateTime> BuildMonths(DateTime fromMonth, DateTime toMonth)
    {
        var months = new List<DateTime>();

        for (var month = fromMonth; month <= toMonth; month = month.AddMonths(1))
        {
            months.Add(month);
        }

        return months;
    }

    private static DateTime FirstDayOfMonth(DateTime value)
    {
        return new DateTime(value.Year, value.Month, 1);
    }
}
```

---

### 4.3. `ExecutivePnLMonthlyActual`

Dùng để chứa số liệu actual của một tháng.

```csharp
internal sealed class ExecutivePnLMonthlyActual
{
    public decimal SalesQuantity { get; set; }
    public decimal SalesAmount { get; set; }
    public decimal CostOfSales { get; set; }
    public decimal FreightAmount { get; set; }
    public decimal DeliveredQuantity { get; set; }
    public decimal ProductionQuantity { get; set; }
    public int OrderCount { get; set; }
    public int OrderLineCount { get; set; }
    public int ProductionOrderCount { get; set; }
}
```

Vai trò:

* Là object trung gian để cộng dồn dữ liệu theo tháng.
* Không trả trực tiếp ra frontend.
* Được `ExecutivePnLActualReader` tạo và cập nhật.
* Được `ExecutivePnLReportBuilder` đọc để build DTO.

---

## 5. Services nội bộ

Các service trong folder `Services` chỉ phục vụ cho query `GetExecutivePnLReport`.

---

### 5.1. `ExecutivePnLActualReader`

Vai trò:

```txt
Đọc dữ liệu từ database
→ group theo tháng
→ cộng dồn vào ExecutivePnLMonthlyActual
```

Class này không build DTO trả frontend. Nó chỉ lo phần lấy số liệu.

Các hàm chính:

```txt
GetActualsAsync()
AddSalesActualsAsync()
AddOrderCountsAsync()
AddDeliveryQuantitiesAsync()
AddFreightAmountsAsync()
AddProductionActualsAsync()
```

Ý nghĩa từng hàm:

#### `GetActualsAsync`

Hàm chính để lấy toàn bộ actual.

```csharp
public async Task<Dictionary<string, ExecutivePnLMonthlyActual>> GetActualsAsync(
    ExecutivePnLFilter filter,
    IReadOnlyList<DateTime> months,
    CancellationToken cancellationToken)
```

Nó tạo dictionary theo tháng:

```txt
actual_2026_01 => ExecutivePnLMonthlyActual
actual_2026_02 => ExecutivePnLMonthlyActual
actual_2026_03 => ExecutivePnLMonthlyActual
```

Sau đó gọi các hàm nhỏ để cộng dữ liệu vào.

---

#### `AddSalesActualsAsync`

Lấy từ `DeliveryRevenueQuery` trên `DeliveryOrderDetails` và group theo tháng tạo phiếu giao hàng.

Cộng các field:

```txt
SalesRevenue
OrderLineCount
```

Giá vốn và số lượng giao vẫn được đọc bởi các hàm riêng để không trộn doanh số đơn hàng với chỉ số vận hành
giao hàng.

---

#### `AddOrderCountsAsync`

Lấy từ `MerchandiseOrders`.

Cộng field:

```txt
OrderCount
```

---

#### `AddDeliveryQuantitiesAsync`

Lấy từ `DeliveryOrderDetails`.

Cộng field:

```txt
DeliveredQuantity
```

---

#### `AddFreightAmountsAsync`

Lấy từ `DeliveryOrders`.

Cộng field:

```txt
FreightAmount
```

---

#### `AddProductionActualsAsync`

Lấy từ `MfgProductionOrders`.

Cộng các field:

```txt
ProductionQuantity
ProductionOrderCount
```

---

### 5.2. `ExecutivePnLReportBuilder`

Vai trò:

```txt
Nhận số liệu đã gom
→ build thành ExecutivePnLReportDto
→ trả ra frontend
```

Class này không query database.

Các hàm chính:

```txt
Build()
BuildColumns()
BuildSections()
BuildLine()
BuildTotal()
```

---

#### `Build`

Hàm chính để dựng toàn bộ report.

```csharp
public ExecutivePnLReportDto Build(
    ExecutivePnLFilter filter,
    IReadOnlyList<DateTime> months,
    IReadOnlyDictionary<string, ExecutivePnLMonthlyActual> actuals)
```

Nó trả về:

```csharp
ExecutivePnLReportDto
```

Bên trong gồm:

```txt
CompanyName
BusinessUnit
Currency
FromMonth
ToMonth
Columns
Sections
```

---

#### `BuildColumns`

Tạo danh sách cột động theo tháng.

Ví dụ:

```txt
actual_2026_01 | 2026-01
actual_2026_02 | 2026-02
actual_2026_03 | 2026-03
total_actual   | Total actual
```

---

#### `BuildSections`

Tạo các nhóm dòng:

```txt
SALES
COST
MARGIN
OPERATIONS
```

---

#### `BuildLine`

Tạo một dòng số liệu.

Ví dụ:

```csharp
BuildLine(
    "TOTAL_SALES",
    "Total sales",
    40,
    months,
    actuals,
    total,
    x => x.SalesAmount,
    isBold: true,
    isSubtotal: true)
```

Dòng này sẽ tạo ra:

```txt
Total sales
  actual_2026_01
  actual_2026_02
  total_actual
```

---

#### `BuildTotal`

Cộng tổng tất cả tháng.

Ví dụ:

```txt
SalesAmount = tổng SalesAmount của các tháng
CostOfSales = tổng CostOfSales của các tháng
FreightAmount = tổng FreightAmount của các tháng
```

---

### 5.3. `ExecutivePnLCalculator`

Vai trò:

```txt
Chứa các công thức tính toán của report
```

Ví dụ:

```csharp
internal static class ExecutivePnLCalculator
{
    public static decimal TotalCost(ExecutivePnLMonthlyActual actual)
    {
        return actual.CostOfSales + actual.FreightAmount;
    }

    public static decimal GrossMargin(ExecutivePnLMonthlyActual actual)
    {
        return actual.SalesAmount - actual.CostOfSales - actual.FreightAmount;
    }

    public static decimal GrossMarginPercent(ExecutivePnLMonthlyActual actual)
    {
        if (actual.SalesAmount == 0)
        {
            return 0;
        }

        return GrossMargin(actual) / actual.SalesAmount * 100;
    }
}
```

Lý do tách calculator:

* Công thức nằm một chỗ.
* Dễ sửa nếu nghiệp vụ đổi.
* Builder không bị chứa quá nhiều phép tính.

---

## 6. Flow xử lý

Flow tổng thể của query:

```txt
GetExecutivePnLReportQueryHandler
  ↓
ExecutivePnLPeriod.Create()
  ↓
Tạo ExecutivePnLFilter
  ↓
ExecutivePnLActualReader.GetActualsAsync()
  ↓
ExecutivePnLReportBuilder.Build()
  ↓
Trả ExecutivePnLReportDto
```

Ví dụ handler sau khi refactor:

```csharp
internal sealed class GetExecutivePnLReportQueryHandler
    : IRequestHandler<GetExecutivePnLReportQuery, ExecutivePnLReportDto>
{
    private readonly ExecutivePnLActualReader _actualReader;
    private readonly ExecutivePnLReportBuilder _reportBuilder;

    public GetExecutivePnLReportQueryHandler(
        ExecutivePnLActualReader actualReader,
        ExecutivePnLReportBuilder reportBuilder)
    {
        _actualReader = actualReader;
        _reportBuilder = reportBuilder;
    }

    public async Task<ExecutivePnLReportDto> Handle(
        GetExecutivePnLReportQuery request,
        CancellationToken cancellationToken)
    {
        var period = ExecutivePnLPeriod.Create(
            request.FromMonth,
            request.ToMonth);

        var filter = new ExecutivePnLFilter
        {
            CompanyId = request.CompanyId,
            BusinessUnit = request.BusinessUnit,
            Currency = request.Currency,
            FromMonth = period.FromMonth,
            ToMonth = period.ToMonth
        };

        var actuals = await _actualReader.GetActualsAsync(
            filter,
            period.Months,
            cancellationToken);

        return _reportBuilder.Build(
            filter,
            period.Months,
            actuals);
    }
}
```

---

## 7. Vì sao không để tất cả trong Handler?

Ban đầu handler làm quá nhiều việc:

```txt
- Xử lý fromMonth / toMonth
- Query nhiều bảng
- Group dữ liệu theo tháng
- Cộng dồn số liệu
- Tính total
- Build columns
- Build sections
- Build lines
- Tính margin
```

Sau khi refactor:

```txt
Handler
= điều phối use case

ActualReader
= đọc DB và gom số

ReportBuilder
= dựng DTO trả frontend

Calculator
= chứa công thức tính

Models
= chứa dữ liệu nội bộ phục vụ query
```

Cách này giúp code:

* Dễ đọc hơn.
* Dễ test hơn.
* Dễ mở rộng thêm dòng report.
* Dễ sửa công thức tính.
* Handler không bị phình quá lớn.

---

## 8. Quy tắc đặt file

Vì các class này chỉ phục vụ riêng query `GetExecutivePnLReport`, nên đặt bên trong:

```txt
Queries/GetExecutivePnLReport
```

Không nên đưa lên root `ExecutivePnL/Services` hoặc `ExecutivePnL/Models` quá sớm.

Quy tắc:

```txt
Dùng cho 1 query
=> để trong folder query đó

Dùng cho nhiều query trong ExecutivePnL
=> đưa lên ExecutivePnL/Models hoặc ExecutivePnL/Services

Dùng cho nhiều report khác nhau
=> đưa vào Reports/Shared

Dùng toàn hệ thống
=> đưa vào Application/Common hoặc Features/Shared
```

---

## 9. Lưu ý khi mở rộng

### Thêm một dòng report mới

Ví dụ muốn thêm dòng `Average order value`, có thể thêm vào `ExecutivePnLCalculator`:

```csharp
public static decimal AverageOrderValue(ExecutivePnLMonthlyActual actual)
{
    if (actual.OrderCount == 0)
    {
        return 0;
    }

    return actual.SalesAmount / actual.OrderCount;
}
```

Sau đó thêm line trong `ExecutivePnLReportBuilder`:

```csharp
BuildLine(
    "AVERAGE_ORDER_VALUE",
    "Average order value",
    50,
    months,
    actuals,
    total,
    ExecutivePnLCalculator.AverageOrderValue)
```

---

### Thêm nguồn dữ liệu mới

Ví dụ muốn lấy thêm chi phí vận hành từ bảng khác, thêm field vào:

```csharp
ExecutivePnLMonthlyActual
```

Sau đó thêm hàm đọc trong `ExecutivePnLActualReader`:

```csharp
AddOperatingExpenseActualsAsync(...)
```

Rồi gọi hàm đó trong `GetActualsAsync`.

---

## 10. Kết luận

Refactor này giúp `GetExecutivePnLReport` đi theo hướng clean hơn:

```txt
Handler chỉ điều phối
Reader chỉ đọc dữ liệu
Builder chỉ dựng report
Calculator chỉ tính công thức
Models chỉ chứa dữ liệu nội bộ
Dtos chỉ trả ra frontend
```

Cách tổ chức này phù hợp với Clean Architecture theo hướng thực chiến, đặc biệt với các màn hình report/dashboard có nhiều query và nhiều logic tổng hợp dữ liệu.
