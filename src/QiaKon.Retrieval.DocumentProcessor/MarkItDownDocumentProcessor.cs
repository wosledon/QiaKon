using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace QiaKon.Retrieval.DocumentProcessor;

/// <summary>
/// 基于 ElBruno.MarkItDotNet 的文档处理器实现
/// 纯 .NET 原生，支持：PDF、Word (.docx)、PowerPoint (.pptx)、Excel (.xlsx)、图片、
/// HTML、CSV、JSON、XML、YAML、RTF、EPUB 等多种格式转换为 Markdown
///
/// 通过反射适配 ElBruno.MarkItDotNet 的具体 API，保证兼容性
/// </summary>
public sealed class MarkItDownDocumentProcessor : IDocumentProcessor
{
    private readonly DocumentProcessorOptions _options;
    private readonly ILogger<MarkItDownDocumentProcessor>? _logger;
    private readonly object? _converterInstance;
    private readonly MethodInfo? _convertFileMethod;
    private readonly MethodInfo? _convertStreamMethod;

    private static readonly HashSet<string> SupportedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        "application/vnd.ms-powerpoint",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/vnd.ms-excel",
        "text/html",
        "text/csv",
        "application/json",
        "application/xml",
        "text/xml",
        "text/yaml",
        "application/rtf",
        "application/epub+zip",
        "image/jpeg",
        "image/png",
        "image/gif",
        "image/webp",
        "image/bmp",
        "image/svg+xml",
        "text/plain",
        "text/markdown",
        "application/zip"
    };

    public IReadOnlyList<string> SupportedMimeTypes => SupportedTypes.ToList();

    public MarkItDownDocumentProcessor(
        IOptions<DocumentProcessorOptions> options,
        ILogger<MarkItDownDocumentProcessor>? logger = null)
    {
        _options = options.Value;
        _logger = logger;
        (_converterInstance, _convertFileMethod, _convertStreamMethod) = InitializeConverter();
    }

    public MarkItDownDocumentProcessor(DocumentProcessorOptions options)
    {
        _options = options;
        (_converterInstance, _convertFileMethod, _convertStreamMethod) = InitializeConverter();
    }

    private static (object? Instance, MethodInfo? FileMethod, MethodInfo? StreamMethod) InitializeConverter()
    {
        try
        {
            var assembly = Assembly.Load("ElBruno.MarkItDotNet");
            var converterType = assembly.GetType("ElBruno.MarkItDotNet.MarkdownConverter");
            if (converterType == null)
                return (null, null, null);

            var instance = Activator.CreateInstance(converterType);

            var fileMethod = converterType.GetMethod("ConvertToMarkdown", new[] { typeof(string) })
                ?? converterType.GetMethod("Convert", new[] { typeof(string) });

            var streamMethod = converterType.GetMethod("ConvertToMarkdown", new[] { typeof(Stream), typeof(string) })
                ?? converterType.GetMethod("Convert", new[] { typeof(Stream), typeof(string) });

            return (instance, fileMethod, streamMethod);
        }
        catch
        {
            return (null, null, null);
        }
    }

    public bool CanProcess(string mimeType)
    {
        if (string.IsNullOrWhiteSpace(mimeType))
            return false;
        return SupportedTypes.Contains(mimeType);
    }

    public async Task<ProcessedDocument> ProcessFileAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("文件不存在", filePath);

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length > _options.MaxFileSize)
            throw new InvalidOperationException($"文件大小 ({fileInfo.Length} bytes) 超过最大限制 ({_options.MaxFileSize} bytes)");

        var mimeType = MimeTypeResolver.Resolve(filePath);
        _logger?.LogDebug("处理文件 {FilePath}, MIME类型: {MimeType}", filePath, mimeType);

        string markdown;

        if (_converterInstance != null && _convertFileMethod != null)
        {
            var result = _convertFileMethod.Invoke(_converterInstance, new object[] { filePath });
            markdown = result?.ToString() ?? string.Empty;
        }
        else
        {
            throw new InvalidOperationException(
                "ElBruno.MarkItDotNet 初始化失败。请确保已安装 ElBruno.MarkItDotNet NuGet 包。");
        }

        return new ProcessedDocument
        {
            Title = Path.GetFileNameWithoutExtension(filePath),
            Content = markdown,
            OriginalMimeType = mimeType,
            ProcessedMimeType = "text/markdown",
            Metadata = new Dictionary<string, object?>
            {
                ["source"] = filePath,
                ["fileSize"] = fileInfo.Length,
                ["processor"] = "ElBruno.MarkItDotNet"
            }
        };
    }

    public async Task<ProcessedDocument> ProcessStreamAsync(
        Stream stream,
        string mimeType,
        string? fileName = null,
        CancellationToken cancellationToken = default)
    {
        if (!CanProcess(mimeType))
            throw new NotSupportedException($"不支持的 MIME 类型: {mimeType}");

        var extension = !string.IsNullOrEmpty(fileName)
            ? Path.GetExtension(fileName)
            : MimeTypeResolver.GetExtension(mimeType);

        _logger?.LogDebug("处理流, MIME类型: {MimeType}, 文件名: {FileName}", mimeType, fileName);

        string markdown;

        if (_converterInstance != null && _convertStreamMethod != null)
        {
            var result = _convertStreamMethod.Invoke(_converterInstance, new object[] { stream, extension });
            markdown = result?.ToString() ?? string.Empty;
        }
        else if (_converterInstance != null && _convertFileMethod != null)
        {
            var tempPath = Path.Combine(Path.GetTempPath(), $"markitdown_{Guid.NewGuid()}{extension}");
            try
            {
                await using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await stream.CopyToAsync(fs, cancellationToken);
                }

                var result = _convertFileMethod.Invoke(_converterInstance, new object[] { tempPath });
                markdown = result?.ToString() ?? string.Empty;
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); } catch { /* 忽略 */ }
                }
            }
        }
        else
        {
            throw new InvalidOperationException(
                "ElBruno.MarkItDotNet 初始化失败。请确保已安装 ElBruno.MarkItDotNet NuGet 包。");
        }

        return new ProcessedDocument
        {
            Title = !string.IsNullOrEmpty(fileName) ? Path.GetFileNameWithoutExtension(fileName) : null,
            Content = markdown,
            OriginalMimeType = mimeType,
            ProcessedMimeType = "text/markdown",
            Metadata = new Dictionary<string, object?>
            {
                ["originalFileName"] = fileName,
                ["processor"] = "ElBruno.MarkItDotNet"
            }
        };
    }
}
