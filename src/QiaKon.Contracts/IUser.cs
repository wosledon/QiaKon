namespace QiaKon.Contracts;

/// <summary>
/// 用户实体接口
/// </summary>
public interface IUser : IEntity, IAuditable
{
    /// <summary>
    /// 用户名
    /// </summary>
    string Username { get; set; }

    /// <summary>
    /// 邮箱
    /// </summary>
    string Email { get; set; }

    /// <summary>
    /// 密码哈希
    /// </summary>
    string PasswordHash { get; set; }

    /// <summary>
    /// 部门 ID
    /// </summary>
    Guid DepartmentId { get; set; }

    /// <summary>
    /// 角色
    /// </summary>
    UserRole Role { get; set; }

    /// <summary>
    /// 是否激活
    /// </summary>
    bool IsActive { get; set; }
}

/// <summary>
/// 部门实体接口
/// </summary>
public interface IDepartment : IEntity, IAuditable, ISoftDelete
{
    /// <summary>
    /// 部门名称
    /// </summary>
    string Name { get; set; }

    /// <summary>
    /// 父部门 ID
    /// </summary>
    Guid? ParentDepartmentId { get; set; }

    /// <summary>
    /// 描述
    /// </summary>
    string? Description { get; set; }
}
