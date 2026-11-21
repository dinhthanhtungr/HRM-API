using HRM.Application.Commons.Searching;

namespace HRM.Application.Features.PLM.SampleRequests.Services.Search;

// Giữ điểm gọi của Sample Request; xử lý chữ dùng chung nằm tại Commons/Searching.
internal static class SampleRequestSearchText
{
    internal static string[] Words(string value) => SearchNameText.Words(value);
    internal static string PhrasePattern(string value) => SearchNameText.PhrasePattern(value);
    internal static string OrderedPattern(string value) => SearchNameText.OrderedPattern(value);
    internal static string AllWordsPattern(string value) => SearchNameText.AllWordsPattern(value);
    internal static string Fold(string value) => SearchNameText.Fold(value);
    internal static string Pattern(string value) => SearchNameText.Pattern(value);
}
