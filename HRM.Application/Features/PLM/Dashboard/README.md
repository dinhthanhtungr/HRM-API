# PLM Dashboard

Thu muc nay chua cac query phuc vu man hinh PLM Dashboard.

## API chinh

- `GET /api/v1/plm/dashboard/summary`
  - Tra tong so yeu cau mau va lenh san xuat theo filter hien tai.
  - Dung cho cac card/tong quan tren dashboard.

- `GET /api/v1/plm/dashboard/monthly-summary`
  - Tra so lieu theo tung thang.
  - Dung cho bang summary dang hien thi: tong / hoan thanh / ty le hoan thanh.

- `GET /api/v1/plm/dashboard/drilldown`
  - Tra danh sach record chi tiet khi nguoi dung click vao mot so lieu trong bang summary.
  - Dung de mo modal/drawer chi tiet, khong reload dashboard chinh.

## Drilldown params

`drilldown` nhan cac filter chung:

- `fromDate`
- `toDate`
- `companyId`
- `productId`
- `customerId`
- `status`
- `monthKey`: dang `yyyy-MM`, vi du `2026-07`
- `source`: `sampleRequests` hoac `productionOrders`
- `completion`: `all`, `finished`, hoac `unfinished`
- `pageNumber`
- `pageSize`
- `keyword`

Vi du:

```http
GET /api/v1/plm/dashboard/drilldown?source=sampleRequests&completion=finished&monthKey=2026-07&pageNumber=1&pageSize=20
```

Click mapping tren FE:

- Click tong yeu cau mau cua thang: `source=sampleRequests&completion=all&monthKey=yyyy-MM`
- Click yeu cau mau hoan thanh: `source=sampleRequests&completion=finished&monthKey=yyyy-MM`
- Click yeu cau mau chua hoan thanh: `source=sampleRequests&completion=unfinished&monthKey=yyyy-MM`
- Click tong lenh san xuat: `source=productionOrders&completion=all&monthKey=yyyy-MM`
- Click lenh san xuat hoan thanh: `source=productionOrders&completion=finished&monthKey=yyyy-MM`
- Click lenh san xuat chua hoan thanh: `source=productionOrders&completion=unfinished&monthKey=yyyy-MM`

## Dashboard rules

Tat ca rule ve source, completion va status nam o:

```txt
Shared/Services/Rules/PLMRules.cs
```

Neu can doi nghiep vu, uu tien sua tai day thay vi hard-code trong tung handler.

Hien tai:

- Sample request finished: `Completed`, `SampleSent`
- Sample request excluded: `Cancelled`
- Production order finished: `Finished`, `Stocked`
- Production order excluded: `Canceled`, `Cancelled`
- Internal customer excluded: `KH_VIETAUS`

Ly do tach rule:

- `summary`, `monthly-summary`, va `drilldown` phai dem cung mot cach.
- Khi click vao so lieu tren dashboard, danh sach drilldown phai khop voi con so da hien thi.
- Khi doi status nghiep vu, chi can sua mot noi.

## Luu y

Neu them API dashboard moi, khong tu khai bao lai cac chuoi nhu `finished`, `sampleRequests`, `productionOrders`.
Hay dung `PLMRules` de tranh lech so lieu giua cac man hinh.
