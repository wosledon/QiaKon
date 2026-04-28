namespace QiaKon.Contracts;

/// <summary>
/// 访问级别枚举
/// </summary>
public enum AccessLevel
{
    /// <summary>
    /// 公开
    /// </summary>
    Public = 0,

    /// <summary>
    /// 内部
    /// </summary>
    Internal = 1,

    /// <summary>
    /// 机密
    /// </summary>
    Confidential = 2,

    /// <summary>
    /// 绝密
    /// </summary>
    Secret = 3
}

/// <summary>
/// 用户角色枚举
/// </summary>
public enum UserRole
{
    /// <summary>
    /// 访客
    /// </summary>
    Guest = 0,

    /// <summary>
    /// 部门成员
    /// </summary>
    DepartmentMember = 1,

    /// <summary>
    /// 部门经理
    /// </summary>
    DepartmentManager = 2,

    /// <summary>
    /// 知识管理员
    /// </summary>
    KnowledgeAdmin = 3,

    /// <summary>
    /// 系统管理员
    /// </summary>
    Admin = 4
}

/// <summary>
/// 文档类型枚举
/// </summary>
public enum DocumentType
{
    /// <summary>
    /// 纯文本
    /// </summary>
    PlainText = 0,

    /// <summary>
    /// Markdown
    /// </summary>
    Markdown = 1,

    /// <summary>
    /// HTML
    /// </summary>
    Html = 2,

    /// <summary>
    /// PDF
    /// </summary>
    Pdf = 3,

    /// <summary>
    /// Word
    /// </summary>
    Word = 4,

    /// <summary>
    /// 表格
    /// </summary>
    Table = 5
}

/// <summary>
/// 索引状态枚举
/// </summary>
public enum IndexStatus
{
    /// <summary>
    /// 待索引
    /// </summary>
    Pending = 0,

    /// <summary>
    /// 索引中
    /// </summary>
    Indexing = 1,

    /// <summary>
    /// 已完成
    /// </summary>
    Completed = 2,

    /// <summary>
    /// 失败
    /// </summary>
    Failed = 3
}
