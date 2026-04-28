# QiaKon.Graph.Engine.Memory - AGENTS.md

> **模块**: 内存图引擎  
> **职责**: 内存中的知识图谱存储与查询  
> **依赖**: `QiaKon.Contracts`, `QiaKon.Graph.Engine`  
> **被依赖**: `QiaKon.Graph.Engine`, `QiaKon.Api`

---

## 一、模块职责

本模块提供基于内存的知识图谱存储与查询实现，适用于开发测试和小规模数据场景。

**核心职责**:
- `MemoryGraphEngine` 实现
- 实体与关系的内存管理
- 图查询（路径、邻居、聚合）
- 并发安全控制

---

## 二、核心实现

### 2.1 MemoryGraphEngine

```csharp
public class MemoryGraphEngine : IGraphEngine
{
    private readonly ConcurrentDictionary<Guid, Entity> _entities = new();
    private readonly ConcurrentDictionary<Guid, Relation> _relations = new();
    private readonly ConcurrentDictionary<Guid, List<Guid>> _adjacencyList = new();
}
```

### 2.2 数据结构

- **实体存储**: `ConcurrentDictionary<Guid, Entity>`
- **关系存储**: `ConcurrentDictionary<Guid, Relation>`
- **邻接表**: `ConcurrentDictionary<Guid, List<Guid>>`

---

## 三、图查询实现

### 3.1 邻居查询

```csharp
public async Task<IReadOnlyList<Entity>> GetNeighborsAsync(
    Guid entityId, 
    int maxHops = 1, 
    CancellationToken ct = default)
{
    var visited = new HashSet<Guid>();
    var queue = new Queue<(Guid id, int depth)>();
    
    queue.Enqueue((entityId, 0));
    visited.Add(entityId);
    
    while (queue.Count > 0)
    {
        var (current, depth) = queue.Dequeue();
        if (depth >= maxHops) continue;
        
        foreach (var neighborId in _adjacencyList.GetValueOrDefault(current, []))
        {
            if (visited.Add(neighborId))
            {
                queue.Enqueue((neighborId, depth + 1));
            }
        }
    }
    
    return visited.Select(id => _entities[id]).ToList();
}
```

### 3.2 路径查询

使用 BFS 查找最短路径。

---

## 四、开发规范

### 4.1 并发安全

- 使用 `ConcurrentDictionary` 保证线程安全
- 批量操作使用锁或事务

### 4.2 性能优化

- 邻接表缓存关系查询
- 使用 BFS 而非 DFS 避免深度过大

---

## 五、测试要求

- 实体增删改查
- 关系增删改查
- 图查询正确性
- 并发安全测试

---

**最后更新**: 2026-04-28  
**维护者**: 后端实现专家 Agent
