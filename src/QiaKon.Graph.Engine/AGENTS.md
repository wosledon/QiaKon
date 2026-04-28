# QiaKon.Graph.Engine - AGENTS.md

> **模块**: 知识图谱引擎  
> **职责**: 知识图谱存储、查询与推理  
> **依赖**: `QiaKon.Contracts`, `QiaKon.Connector.*`  
> **被依赖**: `QiaKon.Api`, `QiaKon.Retrieval.*`

---

## 一、模块职责

本模块提供知识图谱的构建、存储、查询能力，支持结构化知识的推理与检索。

**核心职责**:
- 实体（Entity）管理
- 关系（Relation）管理
- 图查询（路径查询、多跳推理、邻居查询）
- 图聚合与统计

---

## 二、子模块架构

### 2.1 子模块总览

| 子模块                       | 职责                      | 文档                                                 |
| ---------------------------- | ------------------------- | ---------------------------------------------------- |
| `QiaKon.Graph.Engine.Memory` | 内存图存储（开发/测试）   | [AGENTS.md](../QiaKon.Graph.Engine.Memory/AGENTS.md) |
| `QiaKon.Graph.Engine.Npgsql` | PostgreSQL 图存储（生产） | [AGENTS.md](../QiaKon.Graph.Engine.Npgsql/AGENTS.md) |

### 2.2 存储后端对比

| 特性         | Memory           | Npgsql           |
| ------------ | ---------------- | ---------------- |
| **速度**     | 最高速           | 中等             |
| **容量**     | 受内存限制       | 持久化，大容量   |
| **持久化**   | 进程重启丢失     | 持久化存储       |
| **适用场景** | 开发测试、热数据 | 生产环境、冷数据 |

---

## 三、核心概念

### 3.1 图数据模型

| 概念                | 说明                 | 示例                    |
| ------------------- | -------------------- | ----------------------- |
| **实体 (Entity)**   | 知识图谱中的节点     | 公司、产品、人物        |
| **关系 (Relation)** | 实体之间的连接       | 生产、属于、合作        |
| **属性 (Property)** | 实体或关系的额外描述 | 名称、年龄、日期        |
| **三元组 (Triple)** | 主语-谓语-宾语 结构  | (张三)-[工作于]->(腾讯) |

### 3.2 核心接口

```csharp
public interface IGraphEngine
{
    // 实体管理
    Task<Entity> CreateEntityAsync(Entity entity, CancellationToken ct = default);
    Task<Entity?> GetEntityAsync(Guid id, CancellationToken ct = default);
    Task<bool> DeleteEntityAsync(Guid id, CancellationToken ct = default);
    
    // 关系管理
    Task<Relation> CreateRelationAsync(Relation relation, CancellationToken ct = default);
    Task<IReadOnlyList<Relation>> GetRelationsAsync(Guid entityId, CancellationToken ct = default);
    Task<bool> DeleteRelationAsync(Guid id, CancellationToken ct = default);
    
    // 图查询
    Task<IReadOnlyList<Path>> FindPathsAsync(Guid sourceId, Guid targetId, int maxHops, CancellationToken ct = default);
    Task<IReadOnlyList<Entity>> GetNeighborsAsync(Guid entityId, int maxHops = 1, CancellationToken ct = default);
    Task<Dictionary<string, int>> AggregateAsync(string groupBy, CancellationToken ct = default);
}
```

---

## 四、图查询能力

### 4.1 查询类型

| 类型         | 说明                  | 示例                         |
| ------------ | --------------------- | ---------------------------- |
| **关系查询** | 查询实体间关系        | 张三和李四有什么关系？       |
| **多跳推理** | 支持 1~N 跳的关系推理 | 张三的同事的同学在哪里工作？ |
| **属性检索** | 按属性过滤实体        | 所有年龄在 30 岁以上的工程师 |
| **图聚合**   | 统计图结构信息        | 每个部门有多少人？           |

### 4.2 查询流程

```
用户查询
    ↓
Query Parser (自然语言 → 图查询)
    ↓
推理引擎 (多跳推理、规则推理)
    ↓
┌─────────────┬─────────────┬─────────────┐
│  关系查询   │  属性检索   │  图聚合     │
└─────────────┴─────────────┴─────────────┘
    ↓           ↓           ↓
    └───────────┴───────────┘
                ↓
          结果组装
                ↓
         返回结果
```

---

## 五、Memory 实现规范

### 5.1 数据结构

```csharp
public class MemoryGraphEngine : IGraphEngine
{
    private readonly ConcurrentDictionary<Guid, Entity> _entities = new();
    private readonly ConcurrentDictionary<Guid, Relation> _relations = new();
    private readonly ConcurrentDictionary<Guid, List<Guid>> _adjacencyList = new();
}
```

### 5.2 适用场景

- 开发和测试环境
- 小规模数据集（< 10 万实体）
- 需要高速读写的场景
- 热数据缓存层

### 5.3 限制

- 进程重启数据丢失
- 内存占用随数据增长
- 不支持分布式

---

## 六、Npgsql 实现规范

### 6.1 表结构

```sql
-- 实体表
CREATE TABLE entities (
    id UUID PRIMARY KEY,
    name TEXT NOT NULL,
    type TEXT NOT NULL,
    properties JSONB,
    department_id UUID,
    is_public BOOLEAN DEFAULT false,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- 关系表
CREATE TABLE relations (
    id UUID PRIMARY KEY,
    source_id UUID NOT NULL REFERENCES entities(id),
    target_id UUID NOT NULL REFERENCES entities(id),
    type TEXT NOT NULL,
    properties JSONB,
    department_id UUID,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- 索引
CREATE INDEX idx_entities_type ON entities(type);
CREATE INDEX idx_relations_source ON relations(source_id);
CREATE INDEX idx_relations_target ON relations(target_id);
CREATE INDEX idx_relations_type ON relations(type);
```

### 6.2 递归查询

```sql
-- 多跳关系查询（CTE 递归）
WITH RECURSIVE entity_graph AS (
    -- 起始实体
    SELECT id, name, type, 1 AS depth
    FROM entities
    WHERE id = @StartEntityId
    
    UNION ALL
    
    -- 递归查询相邻实体
    SELECT e.id, e.name, e.type, eg.depth + 1
    FROM entities e
    INNER JOIN relations r ON (r.source_id = e.id OR r.target_id = e.id)
    INNER JOIN entity_graph eg ON (r.source_id = eg.id OR r.target_id = eg.id)
    WHERE eg.depth < @MaxDepth
    AND e.id != @StartEntityId
)
SELECT DISTINCT * FROM entity_graph;
```

---

## 七、开发规范

### 7.1 实体命名

- 实体类型：使用 PascalCase（如 `Person`, `Company`）
- 关系类型：使用动词（如 `WorksAt`, `Owns`, `LocatedIn`）

### 7.2 权限过滤

所有查询必须自动注入权限过滤：

```csharp
public async Task<IReadOnlyList<Entity>> QueryAsync(GraphQuery query, UserContext user)
{
    if (!user.IsAdmin)
    {
        query.Filters.Add("department_id", user.DepartmentIds);
        query.Filters.Add("is_public", true);
    }
    
    return await _graphEngine.QueryAsync(query);
}
```

### 7.3 批量操作

```csharp
// 批量创建实体
public async Task<IReadOnlyList<Entity>> CreateEntitiesAsync(
    IReadOnlyList<Entity> entities, 
    CancellationToken ct = default)
{
    using var transaction = await BeginTransactionAsync(ct);
    var results = new List<Entity>();
    
    foreach (var entity in entities)
    {
        results.Add(await CreateEntityAsync(entity, ct));
    }
    
    await transaction.CommitAsync(ct);
    return results;
}
```

---

## 八、测试要求

### 8.1 单元测试

- 实体增删改查
- 关系增删改查
- 图查询逻辑
- 权限过滤逻辑

### 8.2 集成测试

- Memory 引擎完整流程
- Npgsql 引擎完整流程（使用 Testcontainers）
- 多跳推理正确性
- 并发访问安全

---

## 九、注意事项

1. **循环关系**: 检测并处理实体间的循环关系
2. **孤立实体**: 定期清理无关联的孤立实体
3. **索引优化**: 为高频查询字段创建索引
4. **事务管理**: 批量操作使用事务保证一致性

---

**最后更新**: 2026-04-28  
**维护者**: 后端实现专家 Agent
