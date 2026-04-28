# QiaKon.Contracts - AGENTS.md

> **模块**: 通用契约层  
> **职责**: 定义全平台共享的接口和基类  
> **依赖**: 无  
> **被依赖**: 所有业务模块

---

## 一、模块职责

本模块定义全平台通用的接口和基类，确保各模块遵循统一的契约规范。

**核心职责**:
- 定义实体基接口（`IEntity`, `IAuditable`, `ISoftDelete`, `IVersionable`）
- 提供基类实现（`EntityBase`, `AuditableEntityBase`, `SoftDeleteEntityBase`）
- 定义跨模块共享的枚举和 DTO
- 保持轻量，不依赖任何具体实现

---

## 二、核心接口

### 2.1 实体接口

```csharp
// 基础实体接口
public interface IEntity
{
    Guid Id { get; set; }
}

// 审计接口
public interface IAuditable
{
    Guid CreatedBy { get; set; }
    DateTime CreatedAt { get; set; }
    Guid? ModifiedBy { get; set; }
    DateTime? ModifiedAt { get; set; }
}

// 软删除接口
public interface ISoftDelete
{
    bool IsDeleted { get; set; }
}

// 并发版本接口
public interface IVersionable
{
    byte[] RowVersion { get; set; }
}
```

### 2.2 基类实现

| 类名                   | 继承关系                           | 说明                            |
| ---------------------- | ---------------------------------- | ------------------------------- |
| `EntityBase`           | `IEntity`                          | 实现 Id 属性，默认 `new Guid()` |
| `AuditableEntityBase`  | `EntityBase, IAuditable`           | 增加审计字段                    |
| `SoftDeleteEntityBase` | `AuditableEntityBase, ISoftDelete` | 增加软删除字段                  |

---

## 三、开发规范

### 3.1 设计原则

1. **接口隔离**: 每个接口职责单一，避免胖接口
2. **依赖倒置**: 高层模块不依赖低层模块，都依赖抽象
3. **开闭原则**: 对扩展开放，对修改封闭
4. **轻量级**: 本模块仅包含接口和简单基类，不含业务逻辑

### 3.2 命名规范

- 接口：`I` 前缀 + 描述性名称（如 `IEntity`）
- 基类：描述性名称 + `Base` 后缀（如 `EntityBase`）
- 枚举：描述性名称，不使用后缀
- DTO: 描述性名称 + `Dto` 后缀（如 `UserDto`）

### 3.3 添加新接口流程

1. 在对应接口文件中定义接口
2. 如有需要，在基类文件中提供默认实现
3. 更新本文档的接口清单
4. 通知依赖模块更新

---

## 四、实现清单

### 4.1 当前实现

| 文件         | 内容           |
| ------------ | -------------- |
| `IEntity.cs` | `IEntity` 接口 |

### 4.2 待实现

| 接口/类                | 优先级 | 说明           |
| ---------------------- | ------ | -------------- |
| `IAuditable`           | P0     | 审计接口       |
| `ISoftDelete`          | P0     | 软删除接口     |
| `IVersionable`         | P1     | 并发版本接口   |
| `EntityBase`           | P0     | 基础实体实现   |
| `AuditableEntityBase`  | P0     | 审计实体基类   |
| `SoftDeleteEntityBase` | P0     | 软删除实体基类 |

---

## 五、与其他模块的契约

### 5.1 文档管理模块

```csharp
public interface IDocument : IAuditable, ISoftDelete
{
    string Title { get; set; }
    string Content { get; set; }
    Guid DepartmentId { get; set; }
    bool IsPublic { get; set; }
    AccessLevel AccessLevel { get; set; }
    int Version { get; set; }
}
```

### 5.2 知识图谱模块

```csharp
public interface IGraphEntity : IEntity
{
    string Name { get; set; }
    string Type { get; set; }
    Guid DepartmentId { get; set; }
    bool IsPublic { get; set; }
}
```

### 5.3 用户权限模块

```csharp
public interface IUser : IEntity
{
    string Username { get; set; }
    string Email { get; set; }
    Guid DepartmentId { get; set; }
    UserRole Role { get; set; }
}
```

---

## 六、测试要求

- 接口无需单独测试
- 基类实现需编写单元测试验证默认行为
- 测试覆盖率：100%（仅针对基类）

---

## 七、注意事项

1. **破坏性变更**: 接口变更属于破坏性变更，需升级主版本号
2. **向后兼容**: 新增接口成员时，考虑提供默认实现或扩展方法
3. **文档同步**: 接口变更必须同步更新本文档和 FSD.md

---

**最后更新**: 2026-04-28  
**维护者**: 架构师 Agent
