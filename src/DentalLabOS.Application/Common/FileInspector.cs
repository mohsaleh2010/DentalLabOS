using System.Text;

namespace DentalLabOS.Application.Common;

public sealed record InspectedFile(string Kind, string ContentType, string Extension);

/// <summary>
/// Accepts a work-order attachment only when its bytes match an allowed type (plan 5.4: "التحقق من محتوى الملفات ونوعها وحجمها").
/// The extension alone is never trusted.
/// </summary>
public static class FileInspector
{
    public const long MaxBytes = 50L * 1024 * 1024;
    public const int MaxPerWorkOrder = 30;

    public static readonly IReadOnlyList<string> AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp", ".pdf", ".stl", ".ply", ".zip"];

    /// <summary>Returns null when the content doesn't match an allowed type.</summary>
    public static InspectedFile? Inspect(ReadOnlySpan<byte> content, string fileName)
    {
        var ext = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext) || content.Length == 0)
        {
            return null;
        }

        return ext switch
        {
            ".jpg" or ".jpeg" when StartsWith(content, 0xFF, 0xD8, 0xFF) => new("photo", "image/jpeg", ".jpg"),
            ".png" when StartsWith(content, 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A) => new("photo", "image/png", ".png"),
            ".webp" when content.Length > 12 && StartsWith(content, (byte)'R', (byte)'I', (byte)'F', (byte)'F')
                         && content.Slice(8, 4).SequenceEqual("WEBP"u8) => new("photo", "image/webp", ".webp"),
            ".pdf" when StartsWith(content, (byte)'%', (byte)'P', (byte)'D', (byte)'F', (byte)'-') => new("pdf", "application/pdf", ".pdf"),
            ".stl" when IsStl(content) => new("stl", "model/stl", ".stl"),
            ".ply" when StartsWith(content, (byte)'p', (byte)'l', (byte)'y') => new("scan", "application/octet-stream", ".ply"),
            ".zip" when StartsWith(content, 0x50, 0x4B, 0x03, 0x04) => new("scan", "application/zip", ".zip"),
            _ => null,
        };
    }

    /// <summary>ASCII STL starts with "solid"; binary STL is an 80-byte header, a triangle count and 50 bytes per triangle.</summary>
    private static bool IsStl(ReadOnlySpan<byte> content)
    {
        if (content.Length >= 84)
        {
            var triangles = BitConverter.ToUInt32(content.Slice(80, 4));
            if (84L + (50L * triangles) == content.Length)
            {
                return true;
            }
        }

        var head = Encoding.ASCII.GetString(content[..Math.Min(content.Length, 256)]).TrimStart();
        return head.StartsWith("solid", StringComparison.OrdinalIgnoreCase) && Encoding.ASCII.GetString(content[..Math.Min(content.Length, 4096)]).Contains("facet", StringComparison.OrdinalIgnoreCase);
    }

    private static bool StartsWith(ReadOnlySpan<byte> content, params byte[] magic) => content.Length >= magic.Length && content[..magic.Length].SequenceEqual(magic);

    /// <summary>Strips paths and control characters so the stored name is safe to show and to send back in Content-Disposition.</summary>
    public static string SafeName(string fileName)
    {
        var name = Path.GetFileName(fileName ?? string.Empty);
        name = new string(name.Where(c => !char.IsControl(c) && c is not ('"' or '\\' or '/')).ToArray()).Trim();
        return string.IsNullOrEmpty(name) ? "file" : name.Length > 200 ? name[^200..] : name;
    }
}
