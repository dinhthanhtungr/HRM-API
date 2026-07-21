# Executive PnL Chart DTO Convention

## Muc dich

`ExecutivePnLChartDto` duoc dung de tra du lieu chart cho FE ma khong khoa chat vao mot thu vien UI cu the.

BE chi tra du lieu va metadata. FE quyet dinh render bang `BarChart`, `ComposedChart`, `PieChart`, `LineChart`, hoac component khac.

## Cau truc chinh

```csharp
public sealed class ExecutivePnLChartDto
{
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public List<string> Categories { get; set; } = new();
    public List<ExecutivePnLChartCategoryDto> CategoryItems { get; set; } = new();
    public List<ExecutivePnLChartSeriesDto> Series { get; set; } = new();
}
```

## Quy uoc quan trong

Thu tu cua `Categories`, `CategoryItems`, va moi `Series.Data` phai khop nhau theo index.

```text
Categories[i]
CategoryItems[i]
Series[n].Data[i]
```

Tat ca deu dai dien cho cung mot category.

## Categories

`Categories` la danh sach label don gian.

Nen giu field nay de:

- FE cu co the render tiep.
- Chart don gian nhu trend theo thang dung nhanh.
- Debug JSON de hon.

Vi du:

```json
"categories": ["CMR.G1", "CMR.G2"]
```

## CategoryItems

`CategoryItems` la metadata day du cua category.

Dung khi FE can:

- Ve truc co group + item.
- Tooltip day du.
- Click/drill-down bang key.
- Filter/highlight theo group.
- Biet sale/customer/product thuoc group nao.

```csharp
public sealed class ExecutivePnLChartCategoryDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? GroupKey { get; set; }
    public string? GroupLabel { get; set; }
}
```

Vi du:

```json
"categoryItems": [
  {
    "key": "sale-id-1",
    "label": "Nguyen Thi Hoa Loc",
    "groupKey": "group-id-1",
    "groupLabel": "CMR.G1"
  }
]
```

## Series

`Series` la danh sach metric co the render tren chart.

```csharp
public sealed class ExecutivePnLChartSeriesDto
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "bar";
    public List<decimal> Data { get; set; } = new();
}
```

Quy uoc `Type` hien tai:

```text
bar
line
area
pie
reference
```

`reference` dung cho series co gia tri can hien thi/tooltip/label nhung khong nhat thiet ve thanh bar/line mac dinh.

## Vi du FE convert sang Recharts data

```ts
function toRechartsData(chart) {
  return chart.categoryItems.map((category, index) => {
    const item = {
      name: category.label,
      key: category.key,
      groupKey: category.groupKey,
      groupLabel: category.groupLabel,
    };

    chart.series.forEach(series => {
      item[series.key] = series.data[index] ?? 0;
    });

    return item;
  });
}
```

Fallback neu chart chua co `CategoryItems`:

```ts
const categoryItems = chart.categoryItems?.length
  ? chart.categoryItems
  : chart.categories.map(name => ({ key: name, label: name }));
```

## Pie chart

Pie chart co the dung lai chart data co san. FE chon mot series roi map sang `{ name, value }`.

```ts
function toPieData(chart, metricKey) {
  const series = chart.series.find(x => x.key === metricKey);

  return chart.categoryItems.map((category, index) => ({
    name: category.label,
    value: series?.data[index] ?? 0,
  }));
}
```

Khong nen dung `marginPercent` cho pie share, vi percent khong phai gia tri co y nghia cong tong.

## Khi nao them chart moi

Them chart moi khi doi y nghia du lieu:

- Doi dimension: sale, group, customer, product type.
- Doi level: group total, sale detail, top customer.
- Doi period: month, quarter, year trend.
- Doi cach tinh: actual vs target, growth, BOD.

Khong can them chart moi chi vi FE doi cach ve tu bar sang pie/composed.

## Chart hien tai cua By Sales

```text
BY_SALES_REVENUE_PROFIT
- Dimension: sale
- Dung cho top sale revenue/profit

BY_SALES_GROUP_COMPOSED
- Dimension: group
- Dung cho group metrics chart hoac pie theo metric

BY_SALES_DETAIL_METRICS
- Dimension: sale
- Co metadata group + sale
- Dung cho chart nang cao: truc Y group + sale, bar cost/profit, line margin, reference revenue
```

