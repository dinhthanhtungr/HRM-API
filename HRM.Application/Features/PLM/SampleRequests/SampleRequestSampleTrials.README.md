# Sample Request Sample Trials

`SampleRequestSampleTrial` là domain model cho từng lần Lab hoàn thành hoặc gửi mẫu của một `SampleRequest`.

Một yêu cầu phối mẫu có thể có nhiều trial. Nếu khách hàng từ chối hoặc fail mẫu đã gửi, Lab tạo formula/trial mới thay vì sửa đè trial cũ. Report Excel theo tháng lấy mỗi dòng từ `SampleRequestSampleTrial`, không lấy trực tiếp từ `SampleRequest`.

Các snapshot như tên khách hàng, mã yêu cầu, tên sản phẩm, mã màu và loại sản phẩm giữ lại dữ liệu tại thời điểm gửi mẫu để báo cáo lịch sử không đổi khi master data thay đổi sau này.

`DeliveredSampleQuantityKg` là khối lượng giao mẫu thực tế do user nhập tay. Không tự tính field này từ formula, VU hoặc số lượng mẫu yêu cầu trên `SampleRequest`.

## API báo cáo cho FE

```http
GET /api/v1/plm/sample-requests/sample-trials
```

Endpoint bắt đầu từ danh sách Sample Request mà current user được phép xem, sau đó `LEFT JOIN` các trial active:

- Sample Request chưa có trial vẫn trả một dòng với `hasTrial = false`, `sampleRequestSampleTrialId`, `trialNo` và `status` bằng `null`.
- Sample Request có một trial trả một dòng trial.
- Sample Request có nhiều trial trả mỗi trial thành một dòng riêng.
- Khi truyền `status` hoặc `customerReplyStatus`, các dòng chưa có trial không thỏa bộ lọc và sẽ không xuất hiện.

Response trả thêm `sampleRequestStatus` và `requestedSampleQuantity` từ hồ sơ gốc để FE vẫn có dữ liệu hữu ích khi trial chưa được tạo.

Các query parameter:

```text
pageNumber, pageSize, keyword
sampleRequestId, customerId
fromDate, toDate
status, customerReplyStatus
sortBy, sortDirection
```

`fromDate` và `toDate` lọc theo ngày báo cáo ưu tiên lần lượt `finishedDate`, `sentDate`, `requestReceivedDate`, rồi `createdDate`. `toDate` bao gồm trọn ngày được truyền vào.

Các `sortBy` được hỗ trợ:

```text
sampleRequestExternalId
customerName
trialNo
requestReceivedDate
finishedDate
sentDate
updatedDate
```

`turnaroundDays` do backend tính từ `requestReceivedDate` đến `finishedDate` và không trả số âm. Dữ liệu snapshot được ưu tiên để báo cáo lịch sử không thay đổi; nếu snapshot trống, API fallback sang dữ liệu Sample Request/Product/Customer hiện tại.

Khi đã có trial, `status` được serialize thành code chuỗi ổn định (`Draft`, `SampleSent`, `WaitingCustomerFeedback`, `Approved`, `Failed`, `Cancelled`, `PriceQuote`, `ReworkRequested`) để FE tự map label, màu và icon. Khi chưa có trial, `status` là `null` và FE có thể dùng `sampleRequestStatus` để hiển thị trạng thái hồ sơ.

Endpoint có `[Authorize]`, khóa dữ liệu theo company và phạm vi khách hàng bằng `ICustomerVisibilityService.ApplySampleRequestVisibility`. `additiveRate` và `labNote` chỉ được trả cho role thuộc `ApplicationRoleSets.PLM.ProductTechnicalEditors`; user khác nhận `null`.

## Database deployment

Repo hiện không duy trì EF Core migrations history. Script PostgreSQL tạo bảng và index theo đúng EF configuration nằm tại:

```text
HRM.Infrastructure/DatabaseContext/Migrations/20260805_CreateSampleRequestSampleTrials.sql
```

Script chỉ tạo schema/table/index khi chưa tồn tại và không tự import dữ liệu Excel lịch sử.
