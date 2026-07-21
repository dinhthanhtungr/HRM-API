# Work Activity Board

## Mục đích

Feature này cung cấp read model cho panel danh sách khách hàng của màn hình activity board.
Chi tiết task, work plan và interaction được tải độc lập bằng các API CRM có phân trang.

## Phạm vi

- Trả danh sách khách hàng có activity summary để FE chọn customer.
- Không gom toàn bộ task, work plan và interaction vào một response.
- Không cung cấp custom board theo `WorkReferenceType.Custom`.

## API

```http
GET /api/v1/work/activity-board/customers
```

`GET /api/v1/work/activity-board/customers` trả danh sách khách hàng trong visibility scope hiện tại,
kèm số task đang mở, task đã hoàn thành, work plan, interaction, quá hạn và thời điểm hoạt động gần nhất.
API này chỉ phục vụ panel trái và không trả chi tiết từng task/plan/interaction.
Endpoint này dùng `PaginationQuery`, mặc định `pageSize = 15` để FE load thêm bằng infinite scroll.
Backend lọc khách hàng có activity ngay trong database trước khi đếm và phân trang, sau đó mới tính
summary cho tối đa 15 khách của page hiện tại. Vì vậy `onlyHasActivity=true` trả tối đa đủ `pageSize`
khách ở mỗi page và `totalCount` chỉ đếm những khách thật sự có activity mà người dùng được phép thấy.
`activityTypes` chọn nguồn activity cần tính: `FollowUpTask`, `WorkPlan`, `Interaction`. Nếu không truyền
thì API giữ hành vi cũ là tính cả ba nguồn. Nếu truyền, `onlyHasActivity`, `totalCount`,
`openTaskCount`, `completedTaskCount`, `workPlanCount`, `interactionCount`, `overdueCount` và `lastActivityAt`
chỉ dựa trên các nguồn được chọn.
`onlyMine=true` chỉ tính activity do nhân viên hiện tại tạo, được assign trực tiếp hoặc là assignee active
trên task/work plan; interaction được tính khi nhân viên hiện tại tạo hoặc là `AssignedSaleEmployeeId`.
Khách hàng có task chưa hoàn thành được ưu tiên lên trước khi phân trang, sau đó mới sắp xếp theo tên
và mã khách hàng.

Sau khi chọn customer, FE tải từng section độc lập bằng:

```http
GET /api/v1/crm/customers/{customerId}/follow-up-tasks
GET /api/v1/crm/customers/{customerId}/work-plans
GET /api/v1/crm/customers/{customerId}/interactions
```

Ba API CRM dùng `pageNumber` và `pageSize`, vì vậy task, plan và interaction có thể infinite scroll
độc lập mà không tải toàn bộ activity của customer trong một response.

## Dữ liệu

Nguồn dữ liệu chính:

- `WorkTask`, `WorkTaskReference`, `WorkTaskAssignee`.
- `WorkTaskList` gom task vào list/cột cá nhân của employee; `WorkTaskListId` trên task là nullable để giữ tương thích dữ liệu cũ.
- `WorkPlan`, `WorkPlanReference`, `WorkPlanAssignee`.
- `CustomerInteraction`.

Customer summary tính task có `WorkTaskReference` trực tiếp tới customer hoặc gián tiếp qua
`CustomerInteraction`. Work plan dùng customer reference; interaction dùng `CustomerId`.

## Phân quyền và bảo mật

- Mọi query yêu cầu `[Authorize]` từ controller.
- Danh sách luôn áp dụng `CustomerVisibilityService` trước khi đọc activity để tránh IDOR.
- Với lead, nhân viên thường chỉ thấy task/plan/interaction do chính họ tạo, được giao hoặc là assignee.
- Leader có claim group và role full-view được xem rộng hơn theo visibility scope hiện tại.

## Giới hạn hiện tại

- Feature này chỉ đọc dữ liệu summary cho panel khách hàng.
- Customer list summary được tính theo scope hiện tại và dùng để hiển thị panel trái, không thay thế
  API danh sách khách hàng CRM đầy đủ.
- Việc lọc `onlyHasActivity=true` dùng các truy vấn `EXISTS` trên task, plan và interaction. Với tập dữ
  liệu rất lớn cần theo dõi query plan và các index reference/customer để quyết định có cần read model
  tổng hợp riêng hay không.

## Kiểm thử

- Danh sách chỉ trả customer trong visibility scope hiện tại.
- `onlyHasActivity=true` chỉ trả customer có activity mà người dùng được phép thấy.
- Lead với nhân viên thường chỉ được tính activity liên quan nhân viên đó.
- `keyword`, `onlyMine`, `includeCompleted` và `includeInactive` phải hoạt động đúng.
