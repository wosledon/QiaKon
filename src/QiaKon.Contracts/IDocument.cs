namespace QiaKon.Contracts;

/// <summary>
/// 文档实体接口
/// </summary>
public interface IDocument : IEntity, IAuditable, ISoftDelete, IVersionable
{
    /// <summary>
    /// 文档标题
    /// </summary>
    string Title { get; set; }

    /// <summary>
    /// 文档内容（原始文本）
    /// </summary>
    string Content { get; set; }

    /// <summary>
    /// 文档类型
    /// </summary>
    DocumentType Type { get; set; }

    /// <summary>
    /// 部门 ID
    /// </summary>
    Guid DepartmentId { get; set; }

    /// <summary>
    /// 是否公开
    /// </summary>
    bool IsPublic { get; set; }

    /// <summary>
    /// 访问级别
    /// </summary>
    AccessLevel AccessLevel { get; set; }

    /// <summary>
    /// 文档版本号
    /// </summary>
    int Version { get; set; }

    /// <summary>
    /// 索引状态
    /// </summary>
    IndexStatus IndexStatus { get; set; }

    /// <summary>
    /// 索引版本号
    /// </summary>
    int? IndexVersion { get; set; }

    /// <summary>
    /// 元数据（JSON）
    /// </summary>
    string? Metadata { get; set; }
}

/// <summary>
/// 文档块实体接口
/// </summary>
public interface IChunk : IEntity, IAuditable, ISoftDelete
{
    /// <summary>
    /// 所属文档 ID
    /// </summary>
    Guid DocumentId { get; set; }

    /// <summary>
    /// 块内容
    /// </summary>
    string Content { get; set; }

    /// <summary>
    /// 块顺序
    /// </summary>
    int Order { get; set; }

    /// <summary>
    /// 嵌入向量
    /// </summary>
    ReadOnlyMemory<float>? Embedding { get; set; }

    /// <summary>
    /// 分块策略名称
    /// </summary>
    string? ChunkingStrategy { get; set; }

    /// <summary>
    /// 元数据（JSON）
    /// </summary>
    string? Metadata { get; set; }
}
