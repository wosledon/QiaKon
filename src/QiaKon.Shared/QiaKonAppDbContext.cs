using Microsoft.EntityFrameworkCore;
using QiaKon.Contracts;
using QiaKon.EntityFrameworkCore.Npgsql;

namespace QiaKon.Shared;

internal sealed class QiaKonAppDbContext : QiaKonNpgsqlDbContext
{
    public QiaKonAppDbContext(DbContextOptions<QiaKonAppDbContext> options)
        : base(options)
    {
    }

    public DbSet<DocumentRow> Documents => Set<DocumentRow>();
    public DbSet<DocumentChunkRow> DocumentChunks => Set<DocumentChunkRow>();
    public DbSet<GraphEntityRow> GraphEntities => Set<GraphEntityRow>();
    public DbSet<GraphRelationRow> GraphRelations => Set<GraphRelationRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<DocumentRow>(entity =>
        {
            entity.ToTable("documents");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.Title).HasMaxLength(300).IsRequired();
            entity.Property(x => x.Content).HasColumnType("text");
            entity.Property(x => x.MetadataJson).HasColumnType("text");
            entity.Property(x => x.FilePath).HasColumnType("text");
            entity.Property(x => x.IndexErrorMessage).HasColumnType("text");
            entity.HasIndex(x => x.DepartmentId);
            entity.HasIndex(x => x.IndexStatus);
            entity.HasIndex(x => x.CreatedAt);
        });

        modelBuilder.Entity<DocumentChunkRow>(entity =>
        {
            entity.ToTable("document_chunks");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.Content).HasColumnType("text").IsRequired();
            entity.Property(x => x.ChunkingStrategy).HasMaxLength(128);
            entity.HasIndex(x => new { x.DocumentId, x.Order }).IsUnique();
            entity.HasOne<DocumentRow>()
                .WithMany()
                .HasForeignKey(x => x.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GraphEntityRow>(entity =>
        {
            entity.ToTable("graph_entities");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasMaxLength(128).ValueGeneratedNever();
            entity.Property(x => x.Name).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Type).HasMaxLength(128).IsRequired();
            entity.Property(x => x.PropertiesJson).HasColumnType("text");
            entity.HasIndex(x => x.Type);
            entity.HasIndex(x => x.DepartmentId);
            entity.HasIndex(x => x.IsPublic);
        });

        modelBuilder.Entity<GraphRelationRow>(entity =>
        {
            entity.ToTable("graph_relations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasMaxLength(128).ValueGeneratedNever();
            entity.Property(x => x.SourceId).HasMaxLength(128).IsRequired();
            entity.Property(x => x.TargetId).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Type).HasMaxLength(128).IsRequired();
            entity.Property(x => x.PropertiesJson).HasColumnType("text");
            entity.HasIndex(x => x.SourceId);
            entity.HasIndex(x => x.TargetId);
            entity.HasIndex(x => x.Type);
            entity.HasOne<GraphEntityRow>()
                .WithMany()
                .HasForeignKey(x => x.SourceId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<GraphEntityRow>()
                .WithMany()
                .HasForeignKey(x => x.TargetId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}

internal sealed class DocumentRow
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Content { get; set; }
    public DocumentType Type { get; set; }
    public Guid DepartmentId { get; set; }
    public AccessLevel AccessLevel { get; set; }
    public IndexStatus IndexStatus { get; set; }
    public int Version { get; set; }
    public int? IndexVersion { get; set; }
    public string? MetadataJson { get; set; }
    public long Size { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? ModifiedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string? FilePath { get; set; }
    public double? IndexProgress { get; set; }
    public DateTime? IndexStartedAt { get; set; }
    public DateTime? IndexCompletedAt { get; set; }
    public string? IndexErrorMessage { get; set; }
}

internal sealed class DocumentChunkRow
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public string Content { get; set; } = string.Empty;
    public int Order { get; set; }
    public string? ChunkingStrategy { get; set; }
    public DateTime CreatedAt { get; set; }
}

internal sealed class GraphEntityRow
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public Guid DepartmentId { get; set; }
    public bool IsPublic { get; set; }
    public string? PropertiesJson { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

internal sealed class GraphRelationRow
{
    public string Id { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public Guid DepartmentId { get; set; }
    public string? PropertiesJson { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}
