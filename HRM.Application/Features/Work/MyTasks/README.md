# My Tasks

## Contract Hiện Tại

Feature này phục vụ màn `Việc của tôi`: nhân viên hiện tại quản lý list/header cá nhân, tạo personal task không liên quan khách hàng, và đọc chung CRM follow-up task được giao cho mình. Work API chỉ sửa personal task; CRM follow-up task vẫn sửa bằng `/api/v1/crm/follow-up-tasks`.

### Tổ Chức Code

- `Commands/<Action>/<Action>Command.cs`: mỗi action có command và handler riêng.
- `Queries/<Action>/<Action>Query.cs`: mỗi query có query và handler riêng.
- `Dtos/MyTaskDtos.cs`: DTO request/response public cho FE.
- `MyTaskSupport.cs`: helper nghiệp vụ dùng chung trong feature, gồm current employee/company scope và query nhận diện personal/CRM task.
- `MyTaskRules.cs`: rule thuần, giới hạn độ dài, trạng thái terminal và message dùng chung.

Không gom nhiều handler vào một file `MyTaskCommands.cs`, `MyTaskListCommands.cs` hoặc `MyTaskQueries.cs`.

### API Đầy Đủ

```http
GET    /api/v1/work/task-lists
POST   /api/v1/work/task-lists
PATCH  /api/v1/work/task-lists/{listId}
DELETE /api/v1/work/task-lists/{listId}
POST   /api/v1/work/task-lists/reorder

GET    /api/v1/work/tasks/mine
GET    /api/v1/work/tasks/board
POST   /api/v1/work/tasks
GET    /api/v1/work/tasks/{taskId}
PATCH  /api/v1/work/tasks/{taskId}
DELETE /api/v1/work/tasks/{taskId}
POST   /api/v1/work/tasks/{taskId}/complete
POST   /api/v1/work/tasks/reorder
```

Route string trong controller được gom ở `WorkTaskApiRoutes`; sort field string trong `GET /tasks/mine` được gom ở `MyTaskSortFields`; message lỗi được gom ở `MyTaskMessages`.

### Task Lists

`GET /api/v1/work/task-lists` trả list/header active của current employee, sort theo `sortOrder`, sau đó theo `name`.

`POST /api/v1/work/task-lists`

```json
{
  "name": "Hôm nay",
  "sortOrder": 0,
  "isDefault": true
}
```

BE tự set `CompanyId`, `OwnerEmployeeId`, `CreatedDate`, `CreatedBy`. Nếu `isDefault=true`, BE bỏ default cũ của employee đó trước khi tạo list mới.

`PATCH /api/v1/work/task-lists/{listId}`

```json
{
  "name": "Đang làm",
  "sortOrder": 1,
  "isDefault": false,
  "isActive": true
}
```

Chỉ sửa list thuộc current employee và company. Nếu `isActive=false`, list bị deactivate, `isDefault=false` và personal task trong list được chuyển về default list khác nếu có; nếu không có default list thì `WorkTaskListId=null`.

`DELETE /api/v1/work/task-lists/{listId}` soft delete list bằng `IsActive=false`. Không xóa task.

`POST /api/v1/work/task-lists/reorder`

```json
{
  "items": [
    { "listId": "00000000-0000-0000-0000-000000000001", "sortOrder": 0 },
    { "listId": "00000000-0000-0000-0000-000000000002", "sortOrder": 1 }
  ]
}
```

Tất cả list trong payload phải thuộc current employee và company.

### Tasks

`GET /api/v1/work/tasks/mine`

Query hỗ trợ:

- `sourceType`: `All`, `Personal`, `CustomerFollowUp`.
- `workTaskListId`: chỉ lọc personal task.
- `status`, `priority`.
- `keyword`.
- `dueFrom`, `dueTo`.
- `onlyOverdue`.
- `includeCompleted`, mặc định `true`.
- `includeInactive`, mặc định `false`.
- `pageNumber`, `pageSize`, `sortBy`, `sortDirection`.

`sortBy` ổn định gồm `title`, `priority`, `status`, `createddate`, `sortorder`.

`GET /api/v1/work/tasks/board` trả `taskLists`, `unlistedTasks`, `crmFollowUps`.

`GET /api/v1/work/tasks/{taskId}` trả detail personal task của current employee. Endpoint này không trả CRM
follow-up task; nếu calendar trả `sourceType=PersonalTask` thì FE dùng route này, còn `sourceType=FollowUpTask`
thì FE dùng `/api/v1/crm/follow-up-tasks/{taskId}`.

`POST /api/v1/work/tasks`

```json
{
  "workTaskListId": "00000000-0000-0000-0000-000000000001",
  "title": "Gọi lại nhà cung cấp",
  "description": "Nội dung ghi chú",
  "nextAction": "Gọi lúc 15:00",
  "priority": "Normal",
  "dueDate": "2026-07-24T15:00:00",
  "sortOrder": 0
}
```

Tạo personal task độc lập. BE không tạo `WorkTaskReference`, tự set `CompanyId`, `AssignedToEmployeeId=currentEmployeeId`, `CreatedDate`, `CreatedBy`, và tạo `WorkTaskAssignee` primary cho current employee.

`PATCH /api/v1/work/tasks/{taskId}`

```json
{
  "workTaskListId": "00000000-0000-0000-0000-000000000001",
  "title": "Tên mới",
  "description": "Ghi chú mới",
  "nextAction": "Bước tiếp theo",
  "status": "InProgress",
  "priority": "High",
  "dueDate": "2026-07-25T09:00:00",
  "sortOrder": 2,
  "isActive": true
}
```

Chỉ patch personal task của current employee. Nếu status là `Done` hoặc `Canceled`, BE set `CompletedDate` và `CompletedBy`. Nếu chuyển về trạng thái mở, BE clear `CompletedDate`, `CompletedBy`, `CompletionNote`.

`PATCH workTaskListId=null` không dùng để clear list vì DTO nullable không phân biệt field bị bỏ qua và field được gửi null. FE muốn đưa task về nhóm chưa phân loại thì dùng reorder.

`DELETE /api/v1/work/tasks/{taskId}` soft delete personal task bằng `IsActive=false`.

`POST /api/v1/work/tasks/{taskId}/complete`

```json
{
  "status": "Done",
  "completionNote": "Đã xử lý xong"
}
```

`status` chỉ nhận `Done` hoặc `Canceled`.

`POST /api/v1/work/tasks/reorder`

```json
{
  "items": [
    {
      "taskId": "00000000-0000-0000-0000-000000000001",
      "workTaskListId": "00000000-0000-0000-0000-000000000010",
      "sortOrder": 0
    },
    {
      "taskId": "00000000-0000-0000-0000-000000000002",
      "workTaskListId": null,
      "sortOrder": 1
    }
  ]
}
```

Chỉ reorder personal task thuộc current employee. Nếu `workTaskListId` có giá trị, list đó phải thuộc current employee và đang active.

### Response DTO Chính

`MyTaskDto` trả các field chính:

- `workTaskId`, `sourceType`, `workTaskListId`, `sortOrder`.
- `title`, `description`, `nextAction`.
- `status`, `priority`, `dueDate`, `isOverdue`.
- `assignedEmployeeId`, `assignedEmployeeName`.
- `customerId`, `customerExternalId`, `customerName`, `customerInteractionId` cho CRM follow-up task.
- `completedDate`, `completionNote`, `isActive`, `createdDate`, `updatedDate`.

`sourceType=Personal` là task cá nhân. `sourceType=CustomerFollowUp` là CRM task có `WorkTaskReference`.

### Dữ Liệu Và Bảo Mật

- Personal task dùng `Work.WorkTasks`, có `AssignedToEmployeeId` là current employee và không có dòng `WorkTaskReference`.
- CRM follow-up task vẫn là `Work.WorkTasks` nhưng có `WorkTaskReference` tới customer hoặc interaction.
- `WorkTaskListId` trên task nullable để task cũ hoặc task chưa phân loại không bắt buộc nằm trong list.
- `SortOrder` dùng cho kéo thả list và task cá nhân.
- Controller yêu cầu `[Authorize]`.
- Mọi query/command lọc theo `CompanyId` và `EmployeeId` từ current user.
- FE không được set `CompanyId`, `CreatedBy`, `CompletedBy`, owner employee hoặc audit fields.
- PATCH/DELETE task trong Work API chỉ áp dụng cho personal task của current employee, không sửa CRM task.

### Giới Hạn Hiện Tại

- Chưa tạo migration. Database cần có bảng `Work.WorkTaskLists` và các cột `WorkTaskListId`, `SortOrder` trên `Work.WorkTasks` trước khi chạy runtime.
- CRM follow-up task chỉ đọc trong Work API. Nếu cần sửa CRM task, FE phải dùng flow CRM hiện có.

## Mục đích

Feature này cung cấp API cho màn `Việc của tôi`: employee hiện tại có thể tạo list/header cá nhân, tạo task cá nhân không liên quan khách hàng và đọc chung các CRM follow-up task được giao cho mình.

## API

```http
GET    /api/v1/work/task-lists
POST   /api/v1/work/task-lists
PATCH  /api/v1/work/task-lists/{listId}
DELETE /api/v1/work/task-lists/{listId}
POST   /api/v1/work/task-lists/reorder

GET    /api/v1/work/tasks/mine
GET    /api/v1/work/tasks/board
POST   /api/v1/work/tasks
GET    /api/v1/work/tasks/{taskId}
PATCH  /api/v1/work/tasks/{taskId}
DELETE /api/v1/work/tasks/{taskId}
POST   /api/v1/work/tasks/{taskId}/complete
POST   /api/v1/work/tasks/reorder
```

`task-lists` là header/list riêng của current employee. BE tự set `CompanyId`, `OwnerEmployeeId`, audit fields và bảo đảm mỗi employee chỉ có một default list active.

`tasks/mine` trả cả personal task và CRM follow-up task theo `sourceType=All|Personal|CustomerFollowUp`. CRM follow-up task chỉ đọc trong API Work; cập nhật CRM task vẫn dùng các route `/api/v1/crm/follow-up-tasks`.

`tasks/board` trả list cá nhân, task cá nhân theo list, nhóm task chưa phân loại và nhóm ảo `crmFollowUps` để FE render board trong một lần gọi.

## Dữ liệu

- Personal task dùng `Work.WorkTasks`, có `AssignedToEmployeeId` là current employee và không có dòng `WorkTaskReference`.
- CRM follow-up task vẫn là `Work.WorkTasks` nhưng có `WorkTaskReference` tới customer hoặc interaction.
- `WorkTaskListId` trên task nullable để task cũ hoặc task chưa phân loại không bắt buộc nằm trong list.
- `SortOrder` dùng cho kéo thả list và task cá nhân.

## Phân quyền và bảo mật

- Controller yêu cầu `[Authorize]`.
- Mọi query/command lọc theo `CompanyId` và `EmployeeId` từ current user.
- Nếu current user không có employee hoặc company, API trả lỗi rõ ràng.
- FE không được set `CompanyId`, `CreatedBy`, `CompletedBy`, owner employee hoặc audit fields.
- PATCH/DELETE task trong Work API chỉ áp dụng cho personal task của current employee, không sửa CRM task để tránh bỏ qua rule CRM.

## Giới hạn hiện tại

- Bước này chưa tạo migration. Database cần có bảng `Work.WorkTaskLists` và các cột `WorkTaskListId`, `SortOrder` trên `Work.WorkTasks` trước khi chạy runtime.
- PATCH `workTaskListId=null` không dùng để clear list vì DTO nullable không phân biệt field bị bỏ qua và field được gửi null. FE clear list bằng `POST /api/v1/work/tasks/reorder` với `workTaskListId=null`.

## Kiểm thử

- Tạo/sửa/xóa/reorder list chỉ ảnh hưởng current employee.
- Tạo personal task không tạo `WorkTaskReference`, nhưng tạo assignee primary cho current employee.
- `tasks/mine` lọc đúng `sourceType`, status, priority, due date, overdue, keyword, pagination.
- Không patch/delete được personal task của employee hoặc company khác.
- Complete/cancel set đúng `CompletedDate`, `CompletedBy`, `CompletionNote`.
