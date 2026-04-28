using Microsoft.AspNetCore.Mvc;

namespace QiaKon.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentsController : ControllerBase
{
    private static readonly List<Document> _documents = new()
    {
        new Document { Id = Guid.NewGuid(), Title = "Sample Document 1", Content = "Content of document 1", CreatedAt = DateTime.UtcNow },
        new Document { Id = Guid.NewGuid(), Title = "Sample Document 2", Content = "Content of document 2", CreatedAt = DateTime.UtcNow }
    };

    [HttpGet]
    public ApiResponse<IEnumerable<Document>> GetAll()
    {
        return ApiResponse<IEnumerable<Document>>.Ok(_documents);
    }

    [HttpGet("{id:guid}")]
    public ApiResponse<Document> GetById(Guid id)
    {
        var doc = _documents.FirstOrDefault(d => d.Id == id);
        return doc is null
            ? ApiResponse<Document>.Fail("Document not found", 404)
            : ApiResponse<Document>.Ok(doc);
    }

    [HttpPost]
    public ApiResponse<Document> Create([FromBody] CreateDocumentRequest request)
    {
        var doc = new Document
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Content = request.Content,
            CreatedAt = DateTime.UtcNow
        };
        _documents.Add(doc);
        return ApiResponse<Document>.Ok(doc, "Document created");
    }

    [HttpPut("{id:guid}")]
    public ApiResponse<Document> Update(Guid id, [FromBody] UpdateDocumentRequest request)
    {
        var doc = _documents.FirstOrDefault(d => d.Id == id);
        if (doc is null)
            return ApiResponse<Document>.Fail("Document not found", 404);

        doc.Title = request.Title ?? doc.Title;
        doc.Content = request.Content ?? doc.Content;
        doc.UpdatedAt = DateTime.UtcNow;

        return ApiResponse<Document>.Ok(doc, "Document updated");
    }

    [HttpDelete("{id:guid}")]
    public ApiResponse Delete(Guid id)
    {
        var doc = _documents.FirstOrDefault(d => d.Id == id);
        if (doc is null)
            return ApiResponse.Fail("Document not found", 404);

        _documents.Remove(doc);
        return ApiResponse.Ok("Document deleted");
    }
}

public class Document
{
    public Guid Id { get; set; }
    public string? Title { get; set; }
    public string? Content { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public record CreateDocumentRequest(string Title, string Content);
public record UpdateDocumentRequest(string? Title, string? Content);
