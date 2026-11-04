namespace HRM.Application.Features.PLM.Materials.TdsExtraction;

public static class TdsFileReader
{
    public static async Task<byte[]> ReadBoundedAsync(Stream stream, int maxBytes, CancellationToken cancellationToken)
    {
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (output.Length + read > maxBytes) throw new TdsProcessingException("file_size_limit");
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        return output.ToArray();
    }
}
