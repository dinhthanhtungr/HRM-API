namespace HRM.Application.Features.PLM.SampleRequests.Services.Search;

/// <summary>Nhãn chỉ chọn trường tìm, không thay thế company/record scope.</summary>
internal sealed record SampleRequestSearchInput(string? Type, string Keyword)
{
    internal static SampleRequestSearchInput Parse(string? raw)
    {
        var value = raw?.Trim() ?? "";
        var separator = value.IndexOfAny([':', '：']);
        if (separator < 0) return new(null, value);
        var label = SampleRequestSearchText.Fold(string.Join(" ", SampleRequestSearchText.Words(value[..separator])));
        var type = label switch
        {
            "nhan vien" or "ten nhan vien" or "employee" or "employeename" => "employee",
            "nhom" or "ten nhom" or "group" or "groupname" => "group",
            "cong thuc" or "ma cong thuc" or "vu" or "formula" or "formulacode" => "formula",
            "colorcode" or "colourcode" or "color code" or "colour code" or "ma mau" => "colourCode",
            "ten kh" or "ten khach hang" or "customer name" or "customername" => "customer",
            "ma kh" or "ma khach hang" or "customer code" or "customercode" => "customerCode",
            "mst" or "mst kh" or "ma so thue" or "ma so thue kh" or "ma so thue khach hang" or "tax number" or "tax code" or "customer tax number" or "customer tax code" or "customertaxnumber" => "customerTaxNumber",
            "ten san pham" or "ten sp" or "product name" or "productname" => "product",
            "end user" or "enduser" or "nguoi dung cuoi" => "endUser",
            "tp code" or "ma tp" or "tp" or "samplerequestcode" => "sampleRequest",
            _ => null
        };
        return type is null ? new(null, value) : new(type, value[(separator + 1)..].Trim());
    }
}
