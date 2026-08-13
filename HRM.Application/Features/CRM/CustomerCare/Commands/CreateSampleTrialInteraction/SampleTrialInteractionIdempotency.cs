using System.Security.Cryptography;

namespace HRM.Application.Features.CRM.CustomerCare.Commands.CreateSampleTrialInteraction;

internal static class SampleTrialInteractionIdempotency
{
    public static Guid CreateInteractionId(Guid companyId, Guid idempotencyKey)
    {
        Span<byte> input = stackalloc byte[32];
        companyId.TryWriteBytes(input[..16]);
        idempotencyKey.TryWriteBytes(input[16..]);

        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(input, hash);

        Span<byte> guidBytes = stackalloc byte[16];
        hash[..16].CopyTo(guidBytes);
        guidBytes[7] = (byte)((guidBytes[7] & 0x0F) | 0x50);
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80);
        return new Guid(guidBytes);
    }
}
