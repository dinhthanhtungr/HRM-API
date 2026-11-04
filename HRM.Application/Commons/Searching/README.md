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
