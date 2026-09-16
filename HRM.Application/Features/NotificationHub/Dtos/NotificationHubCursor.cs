using System.Buffers.Binary;

namespace HRM.Application.Features.NotificationHub.Dtos;

/// <summary>
/// Opaque cursor cho keyset paging của Notification Hub.
/// </summary>
public static class NotificationHubCursor
{
    private const int PayloadLength = sizeof(long) + 16;

    public static string Encode(DateTime createdDate, Guid notificationId)
    {
        Span<byte> payload = stackalloc byte[PayloadLength];
        BinaryPrimitives.WriteInt64BigEndian(payload, createdDate.ToBinary());
        notificationId.TryWriteBytes(payload[sizeof(long)..], bigEndian: true, out _);

        return Convert.ToBase64String(payload)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static bool TryDecode(string? cursor, out DateTime createdDate, out Guid notificationId)
    {
        createdDate = default;
        notificationId = default;

        if (string.IsNullOrWhiteSpace(cursor))
        {
            return true;
        }

        try
        {
            var normalized = cursor.Trim().Replace('-', '+').Replace('_', '/');
            normalized = normalized.PadRight(normalized.Length + ((4 - normalized.Length % 4) % 4), '=');
            var payload = Convert.FromBase64String(normalized);
            if (payload.Length != PayloadLength)
            {
                return false;
            }

            createdDate = DateTime.FromBinary(BinaryPrimitives.ReadInt64BigEndian(payload));
            notificationId = new Guid(payload.AsSpan(sizeof(long), 16), bigEndian: true);
            return notificationId != Guid.Empty;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
