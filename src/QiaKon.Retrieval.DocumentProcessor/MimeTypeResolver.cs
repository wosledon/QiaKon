namespace QiaKon.Retrieval.DocumentProcessor;

/// <summary>
/// MIME 类型解析器
/// </summary>
public static class MimeTypeResolver
{
    private static readonly Dictionary<string, string> ExtensionMap = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".doc"] = "application/msword",
        [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        [".ppt"] = "application/vnd.ms-powerpoint",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".xls"] = "application/vnd.ms-excel",
        [".html"] = "text/html",
        [".htm"] = "text/html",
        [".csv"] = "text/csv",
        [".json"] = "application/json",
        [".zip"] = "application/zip",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
        [".txt"] = "text/plain",
        [".md"] = "text/markdown",
        [".markdown"] = "text/markdown"
    };

    /// <summary>
    /// 根据文件路径解析 MIME 类型
    /// </summary>
    public static string Resolve(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return ExtensionMap.TryGetValue(ext, out var mimeType)
            ? mimeType
            : "application/octet-stream";
    }

    /// <summary>
    /// 根据文件扩展名解析 MIME 类型
    /// </summary>
    public static string ResolveByExtension(string extension)
    {
        var ext = extension.StartsWith('.') ? extension : $".{extension}";
        return ExtensionMap.TryGetValue(ext, out var mimeType)
            ? mimeType
            : "application/octet-stream";
    }

    /// <summary>
    /// 根据 MIME 类型获取文件扩展名（反向查找）
    /// </summary>
    public static string GetExtension(string mimeType)
    {
        foreach (var pair in ExtensionMap)
        {
            if (pair.Value.Equals(mimeType, StringComparison.OrdinalIgnoreCase))
                return pair.Key;
        }
        return ".bin";
    }
}
