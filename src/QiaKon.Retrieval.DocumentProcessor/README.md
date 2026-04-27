# QiaKon.Retrieval.DocumentProcessor

文档处理模块，负责将各种格式（PDF、Word、Excel、PPT、图片等）转换为标准化的 Markdown 文本。

## 核心依赖

- [ElBruno.MarkItDotNet](https://www.nuget.org/packages/ElBruno.MarkItDotNet) — .NET 原生文档转 Markdown 工具

## 支持的格式

- PDF、Word (.docx)、PowerPoint (.pptx)、Excel (.xlsx)
- HTML、CSV、JSON、XML、YAML、RTF、EPUB
- 图片（.jpg, .png, .gif, .webp, .bmp, .svg）
- 纯文本、Markdown

## 快速开始

```csharp
services.AddMarkItDownDocumentProcessor();
```

然后注入 `IDocumentProcessor` 使用：

```csharp
public class MyService
{
    private readonly IDocumentProcessor _processor;

    public MyService(IDocumentProcessor processor)
    {
        _processor = processor;
    }

    public async Task ProcessDocument(string filePath)
    {
        var result = await _processor.ProcessFileAsync(filePath);
        Console.WriteLine(result.Content); // Markdown 内容
    }
}
```
