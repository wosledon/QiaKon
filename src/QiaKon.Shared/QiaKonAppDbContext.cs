using Microsoft.EntityFrameworkCore;
using QiaKon.Contracts;
using QiaKon.Contracts.DTOs;
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
    public DbSet<DepartmentRow> Departments => Set<DepartmentRow>();
    public DbSet<RoleRow> Roles => Set<RoleRow>();
    public DbSet<UserRow> Users => Set<UserRow>();
    public DbSet<LlmProviderRow> LlmProviders => Set<LlmProviderRow>();
    public DbSet<LlmModelRow> LlmModels => Set<LlmModelRow>();
    public DbSet<SystemConfigRow> SystemConfigs => Set<SystemConfigRow>();
    public DbSet<ConnectorRow> Connectors => Set<ConnectorRow>();
    public DbSet<AuditLogRow> AuditLogs => Set<AuditLogRow>();
    public DbSet<ConversationSessionRow> ConversationSessions => Set<ConversationSessionRow>();
    public DbSet<ConversationMessageRow> ConversationMessages => Set<ConversationMessageRow>();

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

        modelBuilder.Entity<DepartmentRow>(entity =>
        {
            entity.ToTable("departments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.Name).HasMaxLength(128).IsRequired();
            entity.HasIndex(x => x.Name).IsUnique();
            entity.HasIndex(x => x.ParentId);
            entity.HasOne<DepartmentRow>()
                .WithMany()
                .HasForeignKey(x => x.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RoleRow>(entity =>
        {
            entity.ToTable("roles");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.Name).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(512).IsRequired();
            entity.Property(x => x.PermissionsJson).HasColumnType("text");
            entity.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<UserRow>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.Username).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(256).IsRequired();
            entity.Property(x => x.PasswordHash).HasColumnType("text").IsRequired();
            entity.HasIndex(x => x.Username).IsUnique();
            entity.HasIndex(x => x.Email).IsUnique();
            entity.HasIndex(x => x.DepartmentId);
            entity.HasIndex(x => x.Role);
            entity.HasIndex(x => x.IsActive);
            entity.HasOne<DepartmentRow>()
                .WithMany()
                .HasForeignKey(x => x.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LlmProviderRow>(entity =>
        {
            entity.ToTable("llm_providers");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.Name).HasMaxLength(128).IsRequired();
            entity.Property(x => x.BaseUrl).HasMaxLength(1024).IsRequired();
            entity.Property(x => x.ApiKey).HasColumnType("text");
            entity.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<LlmModelRow>(entity =>
        {
            entity.ToTable("llm_models");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.Name).HasMaxLength(128).IsRequired();
            entity.Property(x => x.ActualModelName).HasMaxLength(256).IsRequired();
            entity.HasIndex(x => new { x.ProviderId, x.Name }).IsUnique();
            entity.HasIndex(x => new { x.ModelType, x.IsDefault });
            entity.HasIndex(x => x.IsEnabled);
            entity.HasOne<LlmProviderRow>()
                .WithMany()
                .HasForeignKey(x => x.ProviderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SystemConfigRow>(entity =>
        {
            entity.ToTable("system_configs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.DefaultChunkingStrategy).HasMaxLength(128).IsRequired();
            entity.Property(x => x.CacheStrategy).HasMaxLength(128).IsRequired();
            entity.Property(x => x.PromptTemplate).HasColumnType("text").IsRequired();
        });

        modelBuilder.Entity<ConnectorRow>(entity =>
        {
            entity.ToTable("connectors");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.Name).HasMaxLength(128).IsRequired();
            entity.Property(x => x.BaseUrl).HasMaxLength(1024);
            entity.Property(x => x.ConnectionString).HasColumnType("text");
            entity.Property(x => x.EndpointsJson).HasColumnType("text");
            entity.HasIndex(x => x.Name).IsUnique();
            entity.HasIndex(x => x.Type);
            entity.HasIndex(x => x.State);
        });

        modelBuilder.Entity<AuditLogRow>(entity =>
        {
            entity.ToTable("audit_logs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.Username).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Action).HasMaxLength(128).IsRequired();
            entity.Property(x => x.ResourceType).HasMaxLength(128).IsRequired();
            entity.Property(x => x.ResourceName).HasMaxLength(256);
            entity.Property(x => x.Result).HasMaxLength(64).IsRequired();
            entity.Property(x => x.IpAddress).HasMaxLength(64);
            entity.Property(x => x.Details).HasColumnType("text");
            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => x.Action);
            entity.HasIndex(x => x.Timestamp);
        });

        modelBuilder.Entity<ConversationSessionRow>(entity =>
        {
            entity.ToTable("conversation_sessions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.Title).HasMaxLength(300).IsRequired();
            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => x.CreatedAt);
            entity.HasIndex(x => x.UpdatedAt);
        });

        modelBuilder.Entity<ConversationMessageRow>(entity =>
        {
            entity.ToTable("conversation_messages");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.Role).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Content).HasColumnType("text").IsRequired();
            entity.Property(x => x.SourcesJson).HasColumnType("text");
            entity.HasIndex(x => new { x.ConversationId, x.CreatedAt });
            entity.HasOne<ConversationSessionRow>()
                .WithMany()
                .HasForeignKey(x => x.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
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

internal sealed class DepartmentRow
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public DateTime CreatedAt { get; set; }
}

internal sealed class RoleRow
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsSystem { get; set; }
    public string? PermissionsJson { get; set; }
}

internal sealed class UserRow
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public Guid DepartmentId { get; set; }
    public UserRole Role { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

internal sealed class LlmProviderRow
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public LlmInterfaceType InterfaceType { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public int TimeoutSeconds { get; set; }
    public int RetryCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

internal sealed class LlmModelRow
{
    public Guid Id { get; set; }
    public Guid ProviderId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ActualModelName { get; set; } = string.Empty;
    public LlmModelType ModelType { get; set; }
    public int? VectorDimensions { get; set; }
    public int? MaxTokens { get; set; }
    public bool IsEnabled { get; set; }
    public bool IsDefault { get; set; }
    public bool IsBuiltIn { get; set; }
    public DateTime CreatedAt { get; set; }
}

internal sealed class SystemConfigRow
{
    public Guid Id { get; set; }
    public string DefaultChunkingStrategy { get; set; } = string.Empty;
    public int ChunkSize { get; set; }
    public int ChunkOverlap { get; set; }
    public int DefaultVectorDimensions { get; set; }
    public string CacheStrategy { get; set; } = string.Empty;
    public int CacheExpirationMinutes { get; set; }
    public string PromptTemplate { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

internal sealed class ConnectorRow
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ConnectorType Type { get; set; }
    public ConnectorState State { get; set; }
    public string? BaseUrl { get; set; }
    public string? ConnectionString { get; set; }
    public string? EndpointsJson { get; set; }
    public DateTime? LastHealthCheck { get; set; }
    public DateTime CreatedAt { get; set; }
}

internal sealed class AuditLogRow
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public Guid? ResourceId { get; set; }
    public string? ResourceName { get; set; }
    public string Result { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? Details { get; set; }
    public DateTime Timestamp { get; set; }
}

internal sealed class ConversationSessionRow
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

internal sealed class ConversationMessageRow
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? SourcesJson { get; set; }
    public DateTime CreatedAt { get; set; }
}
