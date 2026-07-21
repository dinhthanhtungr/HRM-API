# Executive PnL Dashboard Builder Notes

## Muc dich

Folder nay chua cac partial file cua `ExecutivePnLDashboardBuilder`.

Builder chi nen lam viec:

- Nhan rows da aggregate tu reader.
- Dung DTO cho table/chart.
- Khong query database.
- Khong chua business filter phuc tap.

## Cac file partial

```text
ExecutivePnLDashboardBuilder.cs
- Entry point Build(...) cho dashboard tong.

ExecutivePnLDashboardBuilder.MetricTabs.cs
- Cac tab metric don gian.
- Trend chart.
- Chart revenue/profit co categories don gian.

ExecutivePnLDashboardBuilder.AnalysisTabs.cs
- Tab analysis chung cho product type/customer.
- Dung metrics va metricValues.

ExecutivePnLDashboardBuilder.SalesTabs.cs
- Logic rieng cho tab by sale.
- Group sale.
- Tong he thong.
- Tong theo group.
- Sale detail.
- Chart theo sale/group.

ExecutivePnLDashboardBuilder.Periods.cs
- Build columns theo month/quarter/year.
- Total actual.
- Growth percent.

ExecutivePnLDashboardBuilder.Charts.cs
- Helper chung cho chart series va category metadata.
```

## By Sales charts

### BY_SALES_REVENUE_PROFIT

Dung cho chart top sale.

```text
Dimension: sale
CategoryItems: sale + group metadata
Series: revenue, profit
```

Goi y FE:

- Horizontal bar chart.
- Bar doanh thu va loi nhuan.
- Tooltip hien group.

### BY_SALES_GROUP_COMPOSED

Dung cho chart tong hop theo group.

```text
Dimension: group
CategoryItems: group metadata
Series: revenue, profit, costOfSales, marginPercent
```

Goi y FE:

- Composed chart.
- Bar revenue/cost/profit.
- Line marginPercent.
- Co the dung lai de ve pie theo revenue/profit/cost.

### BY_SALES_DETAIL_METRICS

Dung cho chart nang cao theo tung sale trong group.

```text
Dimension: sale
CategoryItems: sale + group metadata
Series: revenue, costOfSales, profit, marginPercent
```

Goi y FE:

- Vertical/horizontal composed chart.
- Truc category hien group + sale.
- Bar costOfSales va profit.
- Line marginPercent.
- Revenue dung lam reference/label/tooltip.

### BY_SALES_GROUP_GROWTH

Dung cho positive/negative bar chart tang truong theo group.

```text
Dimension: group
CategoryItems: group metadata
Series: revenueGrowthPercent, profitGrowthPercent, costOfSalesGrowthPercent
```

Y nghia:

- So sanh ky cuoi cung trong filter voi ky lien truoc cung do dai.
- Neu periodType = Month thi so voi thang truoc.
- Neu periodType = Quarter thi so voi quy truoc.
- Neu periodType = Year thi so voi nam truoc.

Goi y FE:

- Bar chart quanh truc 0.
- Duong 0 la moc khong tang/khong giam.
- Gia tri duong la tang truong, gia tri am la suy giam.

### BY_SALES_DETAIL_GROWTH

Dung cho positive/negative bar chart tang truong theo tung sale trong group.

```text
Dimension: sale
CategoryItems: sale + group metadata
Series: revenueGrowthPercent, profitGrowthPercent, costOfSalesGrowthPercent
```

Goi y FE:

- Dung khi giam doc muon drill-down tu group vao ca nhan.
- Co the filter theo group de chart khong qua dai.
- Truc category nen hien group + sale hoac group header.

## Quy tac them chart moi

Chi them chart moi neu doi dataset/dimension/level/cach tinh.

Khong them chart moi chi vi FE doi cach render.

Vi du khong can chart rieng:

```text
Pie group revenue
Composed group metrics
Bar group profit
```

Neu cung dung dimension group va cung series, FE nen dung lai `BY_SALES_GROUP_COMPOSED`.
