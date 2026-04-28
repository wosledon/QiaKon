# QiaKon.Graph.Engine.Npgsql - AGENTS.md

> **模块**: PostgreSQL 图引擎  
> **职责**: 基于 PostgreSQL 的知识图谱持久化存储与查询  
> **依赖**: `QiaKon.Contracts`, `QiaKon.Graph.Engine`, `QiaKon.Connector.Npgsql`  
> **被依赖**: `QiaKon.Graph.Engine`, `QiaKon.Api`

---

## 一、模块职责

本模块使用 PostgreSQL 实现知识图谱的持久化存储，支持大规模数据和复杂图查询。

**核心职责**:
- `NpgsqlGraphEngine` 实现
- 实体与关系表管理
- 递归查询（CTE）
- 图索引优化

---

## 二、数据库表结构

### 2.1 实体表

```sql
CREATE TABLE entities (
    id UUID PRIMARY KEY,
    name TEXT NOT NULL,
    type TEXT NOT NULL,
    properties JSONB,
    department_id UUID,
    is_public BOOLEAN DEFAULT false,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX idx_entities_type ON entities(type);
CREATE INDEX idx_entities_name ON entities USING gin(name gin_trgm_ops);
```

### 2.2 关系表

```sql
CREATE TABLE relations (
    id UUID PRIMARY KEY,
    source_id UUID NOT NULL REFERENCES entities(id),
    target_id UUID NOT NULL REFERENCES entities(id),
    type TEXT NOT NULL,
    properties JSONB,
    department_id UUID,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX idx_relations_source ON relations(source_id);
CREATE INDEX idx_relations_target ON relations(target_id);
CREATE INDEX idx_relations_type ON relations(type);
CREATE INDEX idx_relations_both ON relations(source_id, target_id);
```

---

## 三、图查询实现

### 3.1 多跳查询（CTE 递归）

```sql
WITH RECURSIVE entity_graph AS (
    SELECT id, name, type, 1 AS depth
    FROM entities
    WHERE id = @StartEntityId
    
    UNION ALL
    
    SELECT e.id, e.name, e.type, eg.depth + 1
    FROM entities e
    INNER JOIN relations r ON (r.source_id = e.id OR r.target_id = e.id)
    INNER JOIN entity_graph eg ON (r.source_id = eg.id OR r.target_id = eg.id)
    WHERE eg.depth < @MaxDepth
    AND e.id != @StartEntityId
)
SELECT DISTINCT * FROM entity_graph;
```

### 3.2 路径查询

使用 CTE 递归查找两个实体间的路径。

---

## 四、开发规范

### 4.1 GraphEngineOptions

```csharp
public class GraphEngineOptions
{
    public string ConnectionString { get; set; } = string.Empty;
    public int CommandTimeout { get; set; } = 30;
    public int MaxPoolSize { get; set; } = 100;
    public int DefaultMaxHops { get; set; } = 3;
}
```

### 4.2 权限过滤

所有查询自动注入权限过滤：

```sql
WHERE (department_id = @DepartmentId OR is_public = true)
```

---

## 五、性能优化

### 5.1 索引策略

- 实体类型索引
- 关系源/目标索引
- 全文搜索索引（GIN）

### 5.2 查询优化

- 限制最大跳数
- 使用 EXPLAIN ANALYZE 分析慢查询
- 批量操作使用事务

---

## 六、测试要求

- CRUD 操作
- 递归查询正确性
- 权限过滤
- 性能基准测试

---

**最后更新**: 2026-04-28  
**维护者**: 后端实现专家 Agent
