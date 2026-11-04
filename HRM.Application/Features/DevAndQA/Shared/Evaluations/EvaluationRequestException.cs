namespace HRM.Application.Features.DevAndQA.Shared.Evaluations;

/// <summary>Lỗi nghiệp vụ có mã HTTP ổn định; không đưa lỗi DB ra client.</summary>
public sealed class EvaluationRequestException(string message, int statusCode = 400, Exception? innerException = null)
    : Exception(message, innerException)
{
    public int StatusCode { get; } = statusCode;
}
