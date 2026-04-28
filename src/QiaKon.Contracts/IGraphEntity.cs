namespace QiaKon.Contracts;

/// <summary>
/// 图谱实体接口
/// </summary>
public interface IGraphEntity : IEntity, IAuditable, ISoftDelete
{
    /// <summary>
    /// 实体名称
    /// </summary>
    string Name { get; set; }

    /// <summary>
    /// 实体类型
    /// </summary>
    string Type { get; set; }

    /// <summary>
    /// 部门 ID
    /// </summary>
    Guid DepartmentId { get; set; }

    /// <summary>
    /// 是否公开
    /// </summary>
    bool IsPublic { get; set; }

    /// <summary>
    /// 属性（JSON）
    /// </summary>
    string? Properties { get; set; }
}

/// <summary>
/// 图谱关系接口
/// </summary>
public interface IGraphRelation : IEntity, IAuditable, ISoftDelete
{
    /// <summary>
    /// 源实体 ID
    /// </summary>
    Guid SourceId { get; set; }

    /// <summary>
    /// 目标实体 ID
    /// </summary>
    Guid TargetId { get; set; }

    /// <summary>
    /// 关系类型
    /// </summary>
    string Type { get; set; }

    /// <summary>
    /// 部门 ID
    /// </summary>
    Guid DepartmentId { get; set; }

    /// <summary>
    /// 属性（JSON）
    /// </summary>
    string? Properties { get; set; }
}
