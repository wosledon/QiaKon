using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using QiaKon.Contracts;
using QiaKon.Contracts.DTOs;

namespace QiaKon.Shared;

/// <summary>
/// 内存态图谱服务实现（带种子数据）
/// </summary>
public sealed class MemoryGraphService : IGraphService
{
    private readonly Dictionary<string, GraphEntityRecord> _entities = new();
    private readonly Dictionary<string, GraphRelationRecord> _relations = new();
    private readonly Dictionary<Guid, string> _departments = new();
    private readonly ILogger<MemoryGraphService>? _logger;

    public MemoryGraphService(ILogger<MemoryGraphService>? logger = null)
    {
        _logger = logger;
        InitializeSeedData();
    }

    private void InitializeSeedData()
    {
        _departments[Guid.Parse("11111111-1111-1111-1111-111111111111")] = "研发部";
        _departments[Guid.Parse("22222222-2222-2222-2222-222222222222")] = "销售部";
        _departments[Guid.Parse("33333333-3333-3333-3333-333333333333")] = "人力资源部";

        var adminId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var engineering = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var sales = Guid.Parse("22222222-2222-2222-2222-222222222222");

        AddEntity(new GraphEntityRecord("entity_001", "QiaKon平台", "Platform", engineering, true, new JsonObject { ["description"] = "企业级KAG平台", ["version"] = "1.0" }, adminId, DateTime.UtcNow.AddDays(-30)));
        AddEntity(new GraphEntityRecord("entity_002", "RAG检索模块", "Module", engineering, true, new JsonObject { ["description"] = "检索增强生成模块", ["technology"] = "pgvector" }, adminId, DateTime.UtcNow.AddDays(-25)));
        AddEntity(new GraphEntityRecord("entity_003", "知识图谱引擎", "Module", engineering, true, new JsonObject { ["description"] = "知识图谱存储与查询引擎", ["storage"] = "Memory/Npgsql" }, adminId, DateTime.UtcNow.AddDays(-25)));
        AddEntity(new GraphEntityRecord("entity_004", ".NET 9", "Technology", engineering, true, new JsonObject { ["company"] = "Microsoft" }, adminId, DateTime.UtcNow.AddDays(-20)));
        AddEntity(new GraphEntityRecord("entity_005", "PostgreSQL", "Database", engineering, true, new JsonObject { ["features"] = "pgvector" }, adminId, DateTime.UtcNow.AddDays(-20)));
        AddEntity(new GraphEntityRecord("entity_006", "Redis", "Cache", engineering, false, new JsonObject { ["description"] = "分布式缓存" }, adminId, DateTime.UtcNow.AddDays(-15)));
        AddEntity(new GraphEntityRecord("entity_007", "张伟", "Person", engineering, false, new JsonObject { ["title"] = "研发经理", ["email"] = "zhangwei@qiakon.com" }, adminId, DateTime.UtcNow.AddDays(-10)));
        AddEntity(new GraphEntityRecord("entity_008", "李娜", "Person", sales, false, new JsonObject { ["title"] = "销售总监", ["email"] = "lina@qiakon.com" }, adminId, DateTime.UtcNow.AddDays(-10)));
        AddEntity(new GraphEntityRecord("entity_009", "KAG融合架构", "Concept", engineering, true, new JsonObject { ["description"] = "知识图谱与RAG深度融合架构" }, adminId, DateTime.UtcNow.AddDays(-5)));
        AddEntity(new GraphEntityRecord("entity_010", "向量检索", "Technology", engineering, true, new JsonObject { ["description"] = "基于向量相似度的检索技术" }, adminId, DateTime.UtcNow.AddDays(-5)));

        AddRelation(new GraphRelationRecord("rel_001", "entity_001", "entity_002", "CONTAINS", engineering, new JsonObject(), adminId, DateTime.UtcNow.AddDays(-25)));
        AddRelation(new GraphRelationRecord("rel_002", "entity_001", "entity_003", "CONTAINS", engineering, new JsonObject(), adminId, DateTime.UtcNow.AddDays(-25)));
        AddRelation(new GraphRelationRecord("rel_003", "entity_001", "entity_009", "IMPLEMENTS", engineering, new JsonObject(), adminId, DateTime.UtcNow.AddDays(-5)));
        AddRelation(new GraphRelationRecord("rel_004", "entity_002", "entity_010", "USES", engineering, new JsonObject(), adminId, DateTime.UtcNow.AddDays(-5)));
        AddRelation(new GraphRelationRecord("rel_005", "entity_002", "entity_005", "USES", engineering, new JsonObject(), adminId, DateTime.UtcNow.AddDays(-20)));
        AddRelation(new GraphRelationRecord("rel_006", "entity_003", "entity_005", "USES", engineering, new JsonObject(), adminId, DateTime.UtcNow.AddDays(-20)));
        AddRelation(new GraphRelationRecord("rel_007", "entity_001", "entity_004", "BUILT_WITH", engineering, new JsonObject(), adminId, DateTime.UtcNow.AddDays(-20)));
        AddRelation(new GraphRelationRecord("rel_008", "entity_001", "entity_006", "USES", engineering, new JsonObject(), adminId, DateTime.UtcNow.AddDays(-15)));
        AddRelation(new GraphRelationRecord("rel_009", "entity_007", "entity_001", "MANAGES", engineering, new JsonObject(), adminId, DateTime.UtcNow.AddDays(-10)));
        AddRelation(new GraphRelationRecord("rel_010", "entity_008", "entity_001", "SUPPORTS", sales, new JsonObject(), adminId, DateTime.UtcNow.AddDays(-10)));
    }

    public EntityPagedResultDto GetEntities(string? label, int offset, int limit)
    {
        var query = _entities.Values.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(label))
        {
            query = query.Where(e => e.Type.Contains(label, StringComparison.OrdinalIgnoreCase) || e.Name.Contains(label, StringComparison.OrdinalIgnoreCase));
        }

        var totalCount = query.LongCount();
        var items = query.OrderBy(e => e.Name).Skip(offset).Take(limit).Select(ToDto).ToList();
        return new EntityPagedResultDto(items, totalCount, offset, limit);
    }

    public EntityPagedResultDto GetEntitiesFiltered(string? name, string? type, Guid? departmentId, bool? isPublic, int offset, int limit)
    {
        var query = _entities.Values.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(name))
        {
            query = query.Where(e => e.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(e => e.Type.Equals(type, StringComparison.OrdinalIgnoreCase));
        }

        if (departmentId.HasValue)
        {
            query = query.Where(e => e.DepartmentId == departmentId.Value);
        }

        if (isPublic.HasValue)
        {
            query = query.Where(e => e.IsPublic == isPublic.Value);
        }

        var totalCount = query.LongCount();
        var items = query.OrderBy(e => e.Name).Skip(offset).Take(limit).Select(ToDto).ToList();
        return new EntityPagedResultDto(items, totalCount, offset, limit);
    }

    public EntityDetailDto? GetEntity(string id)
    {
        if (!_entities.TryGetValue(id, out var entity))
            return null;

        var neighbors = _relations.Values
            .Where(r => r.SourceId == id || r.TargetId == id)
            .Select(r => new NeighborDto(
                ToDto(_entities[r.SourceId == id ? r.TargetId : r.SourceId]),
                r.Type,
                r.SourceId == id ? "outgoing" : "incoming"))
            .ToList();

        return new EntityDetailDto(ToDto(entity), neighbors, neighbors.Count);
    }

    public GraphEntityDto CreateEntity(CreateEntityRequestDto request, Guid userId)
    {
        var departmentId = request.DepartmentId ?? Guid.Parse("11111111-1111-1111-1111-111111111111");
        var entity = new GraphEntityRecord(
            $"entity_{Guid.NewGuid():N}",
            request.Name,
            request.Type,
            departmentId,
            (request.AccessLevel ?? AccessLevel.Department) == AccessLevel.Public,
            request.Properties is null ? new JsonObject() : JsonSerializer.SerializeToNode(request.Properties) as JsonObject ?? new JsonObject(),
            userId,
            DateTime.UtcNow);

        AddEntity(entity);
        return ToDto(entity);
    }

    public GraphEntityDto? UpdateEntity(string id, UpdateEntityRequestDto request)
    {
        if (!_entities.TryGetValue(id, out var entity))
            return null;

        entity.Name = string.IsNullOrWhiteSpace(request.Name) ? entity.Name : request.Name;
        entity.Type = string.IsNullOrWhiteSpace(request.Type) ? entity.Type : request.Type;
        if (request.Properties is not null)
        {
            entity.Properties = JsonSerializer.SerializeToNode(request.Properties) as JsonObject ?? new JsonObject();
        }

        entity.UpdatedAt = DateTime.UtcNow;
        return ToDto(entity);
    }

    public bool DeleteEntity(string id)
    {
        if (!_entities.Remove(id))
            return false;

        var relationIds = _relations.Values.Where(r => r.SourceId == id || r.TargetId == id).Select(r => r.Id).ToList();
        foreach (var relationId in relationIds)
        {
            _relations.Remove(relationId);
        }

        return true;
    }

    public RelationListResultDto GetRelations(int offset, int limit, string? type = null)
    {
        var query = _relations.Values.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(r => r.Type.Contains(type, StringComparison.OrdinalIgnoreCase));
        }

        var totalCount = query.LongCount();
        var items = query.OrderBy(r => r.Type).Skip(offset).Take(limit).Select(ToRelationDto).ToList();
        return new RelationListResultDto(items, totalCount);
    }

    public RelationListResultDto GetRelationsFiltered(string? type, string? sourceEntityId, string? targetEntityId, int offset, int limit)
    {
        var query = _relations.Values.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(r => r.Type.Contains(type, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(sourceEntityId))
        {
            query = query.Where(r => r.SourceId == sourceEntityId);
        }

        if (!string.IsNullOrWhiteSpace(targetEntityId))
        {
            query = query.Where(r => r.TargetId == targetEntityId);
        }

        var totalCount = query.LongCount();
        var items = query.OrderBy(r => r.Type).Skip(offset).Take(limit).Select(ToRelationDto).ToList();
        return new RelationListResultDto(items, totalCount);
    }

    public GraphRelationDto CreateRelation(CreateRelationRequestDto request, Guid userId)
    {
        var departmentId = _entities.TryGetValue(request.SourceId, out var source) ? source.DepartmentId : Guid.Empty;
        var relation = new GraphRelationRecord(
            $"rel_{Guid.NewGuid():N}",
            request.SourceId,
            request.TargetId,
            request.Type,
            departmentId,
            request.Properties is null ? new JsonObject() : JsonSerializer.SerializeToNode(request.Properties) as JsonObject ?? new JsonObject(),
            userId,
            DateTime.UtcNow);

        AddRelation(relation);
        return ToRelationDto(relation);
    }

    public bool DeleteRelation(string id)
    {
        return _relations.Remove(id);
    }

    public GraphQueryResponseDto Query(GraphQueryRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.StartEntityId) || string.IsNullOrWhiteSpace(request.EndEntityId))
        {
            return new GraphQueryResponseDto(Array.Empty<GraphPathDto>());
        }

        if (!_entities.ContainsKey(request.StartEntityId) || !_entities.ContainsKey(request.EndEntityId))
        {
            return new GraphQueryResponseDto(Array.Empty<GraphPathDto>());
        }

        var paths = new List<GraphPathDto>();

        var direct = _relations.Values
            .Where(r => r.SourceId == request.StartEntityId && r.TargetId == request.EndEntityId)
            .Where(r => string.IsNullOrWhiteSpace(request.RelationType) || r.Type.Equals(request.RelationType, StringComparison.OrdinalIgnoreCase))
            .Select(r => new GraphPathDto(
                new[] { ToDto(_entities[r.SourceId]), ToDto(_entities[r.TargetId]) },
                new[] { ToRelationDto(r) },
                1))
            .ToList();

        paths.AddRange(direct);

        if (paths.Count == 0 && request.MaxHops >= 2)
        {
            var twoHop = from first in _relations.Values
                         where first.SourceId == request.StartEntityId
                         from second in _relations.Values
                         where second.SourceId == first.TargetId && second.TargetId == request.EndEntityId
                         where string.IsNullOrWhiteSpace(request.RelationType)
                            || first.Type.Equals(request.RelationType, StringComparison.OrdinalIgnoreCase)
                            || second.Type.Equals(request.RelationType, StringComparison.OrdinalIgnoreCase)
                         select new GraphPathDto(
                             new[] { ToDto(_entities[first.SourceId]), ToDto(_entities[first.TargetId]), ToDto(_entities[second.TargetId]) },
                             new[] { ToRelationDto(first), ToRelationDto(second) },
                             2);

            paths.AddRange(twoHop);
        }

        return new GraphQueryResponseDto(paths);
    }

    /// <summary>
    /// BFS路径查询
    /// </summary>
    public PathQueryResultDto FindPaths(string sourceId, string targetId, int maxPaths, int maxHops)
    {
        if (!_entities.ContainsKey(sourceId) || !_entities.ContainsKey(targetId))
        {
            return new PathQueryResultDto(Array.Empty<GraphPathDto>(), 0);
        }

        var paths = new List<GraphPathDto>();
        var visited = new Dictionary<string, int>();
        var queue = new Queue<List<(string EntityId, string RelationId, string Direction)>>();

        queue.Enqueue(new List<(string, string, string)> { (sourceId, "", "") });
        visited[sourceId] = 0;

        while (queue.Count > 0 && paths.Count < maxPaths)
        {
            var currentPath = queue.Dequeue();
            var currentId = currentPath.Last().EntityId;
            var currentDepth = currentPath.Count - 1;

            if (currentDepth >= maxHops)
                continue;

            var outgoingRelations = _relations.Values.Where(r => r.SourceId == currentId);
            var incomingRelations = _relations.Values.Where(r => r.TargetId == currentId);

            foreach (var rel in outgoingRelations)
            {
                var newPath = new List<(string, string, string)>(currentPath) { (rel.TargetId, rel.Id, "outgoing") };
                if (rel.TargetId == targetId)
                {
                    var pathDto = BuildPathDto(newPath);
                    if (pathDto.TotalHops <= maxHops)
                        paths.Add(pathDto);
                }
                else if (!visited.ContainsKey(rel.TargetId) || visited[rel.TargetId] > currentDepth + 1)
                {
                    visited[rel.TargetId] = currentDepth + 1;
                    queue.Enqueue(newPath);
                }
            }

            foreach (var rel in incomingRelations)
            {
                var newPath = new List<(string, string, string)>(currentPath) { (rel.SourceId, rel.Id, "incoming") };
                if (rel.SourceId == targetId)
                {
                    var pathDto = BuildPathDto(newPath);
                    if (pathDto.TotalHops <= maxHops)
                        paths.Add(pathDto);
                }
                else if (!visited.ContainsKey(rel.SourceId) || visited[rel.SourceId] > currentDepth + 1)
                {
                    visited[rel.SourceId] = currentDepth + 1;
                    queue.Enqueue(newPath);
                }
            }
        }

        return new PathQueryResultDto(paths, paths.Count);
    }

    /// <summary>
    /// 多跳推理查询
    /// </summary>
    public MultiHopQueryResultDto MultiHopQuery(string startId, int maxHops, IReadOnlyList<string>? relationTypes = null)
    {
        if (!_entities.ContainsKey(startId))
        {
            return new MultiHopQueryResultDto(startId, Array.Empty<ReachableEntityDto>(), 0);
        }

        var reachableEntities = new Dictionary<string, ReachableEntityDto>();
        var visited = new Dictionary<string, int>();
        var queue = new Queue<(string EntityId, int Depth, List<string> PathRels)>();

        queue.Enqueue((startId, 0, new List<string>()));
        visited[startId] = 0;

        while (queue.Count > 0)
        {
            var (currentId, depth, pathRels) = queue.Dequeue();

            if (depth >= maxHops)
                continue;

            var outgoingRelations = _relations.Values.Where(r => r.SourceId == currentId);
            var incomingRelations = _relations.Values.Where(r => r.TargetId == currentId);

            foreach (var rel in outgoingRelations)
            {
                if (relationTypes is not null && relationTypes.Count > 0 &&
                    !relationTypes.Contains(rel.Type, StringComparer.OrdinalIgnoreCase))
                    continue;

                var newDepth = depth + 1;
                var newPathRels = new List<string>(pathRels) { rel.Type };

                if (!visited.ContainsKey(rel.TargetId) || visited[rel.TargetId] > newDepth)
                {
                    visited[rel.TargetId] = newDepth;
                    queue.Enqueue((rel.TargetId, newDepth, newPathRels));

                    reachableEntities[rel.TargetId] = new ReachableEntityDto(
                        ToDto(_entities[rel.TargetId]),
                        newDepth,
                        newPathRels);
                }
            }

            foreach (var rel in incomingRelations)
            {
                if (relationTypes is not null && relationTypes.Count > 0 &&
                    !relationTypes.Contains(rel.Type, StringComparer.OrdinalIgnoreCase))
                    continue;

                var newDepth = depth + 1;
                var newPathRels = new List<string>(pathRels) { rel.Type };

                if (!visited.ContainsKey(rel.SourceId) || visited[rel.SourceId] > newDepth)
                {
                    visited[rel.SourceId] = newDepth;
                    queue.Enqueue((rel.SourceId, newDepth, newPathRels));

                    reachableEntities[rel.SourceId] = new ReachableEntityDto(
                        ToDto(_entities[rel.SourceId]),
                        newDepth,
                        newPathRels);
                }
            }
        }

        var result = reachableEntities.Values.OrderBy(x => x.MinHops).ToList();
        return new MultiHopQueryResultDto(startId, result, result.Count);
    }

    /// <summary>
    /// 邻居查询
    /// </summary>
    public NeighborsQueryResultDto FindNeighbors(string entityId, string direction, int limit)
    {
        if (!_entities.ContainsKey(entityId))
        {
            return new NeighborsQueryResultDto(entityId, Array.Empty<NeighborDto>(), 0);
        }

        var neighbors = new List<NeighborDto>();

        if (direction is "outgoing" or "both")
        {
            foreach (var rel in _relations.Values.Where(r => r.SourceId == entityId))
            {
                neighbors.Add(new NeighborDto(
                    ToDto(_entities[rel.TargetId]),
                    rel.Type,
                    "outgoing"));
            }
        }

        if (direction is "incoming" or "both")
        {
            foreach (var rel in _relations.Values.Where(r => r.TargetId == entityId))
            {
                neighbors.Add(new NeighborDto(
                    ToDto(_entities[rel.SourceId]),
                    rel.Type,
                    "incoming"));
            }
        }

        var limited = neighbors.Take(limit).ToList();
        return new NeighborsQueryResultDto(entityId, limited, neighbors.Count);
    }

    /// <summary>
    /// 聚合查询
    /// </summary>
    public AggregateQueryResultDto AggregateQuery(string groupBy, AggregateFilterDto? filters = null)
    {
        var entityList = _entities.Values.AsEnumerable();

        if (filters is not null)
        {
            if (filters.EntityTypes is not null && filters.EntityTypes.Count > 0)
            {
                entityList = entityList.Where(e => filters.EntityTypes.Contains(e.Type, StringComparer.OrdinalIgnoreCase));
            }
            if (filters.DepartmentId.HasValue)
            {
                entityList = entityList.Where(e => e.DepartmentId == filters.DepartmentId.Value);
            }
            if (filters.IsPublic.HasValue)
            {
                entityList = entityList.Where(e => e.IsPublic == filters.IsPublic.Value);
            }
        }

        List<AggregateGroupDto> groups;
        long totalCount;

        if (groupBy.Equals("type", StringComparison.OrdinalIgnoreCase))
        {
            var typeGroups = entityList.GroupBy(e => e.Type);
            totalCount = entityList.LongCount();
            groups = typeGroups.Select(g => new AggregateGroupDto(
                g.Key,
                g.LongCount(),
                totalCount > 0 ? (double)g.LongCount() / totalCount * 100 : 0)).ToList();
        }
        else if (groupBy.Equals("department", StringComparison.OrdinalIgnoreCase))
        {
            var deptGroups = entityList.GroupBy(e => e.DepartmentId);
            totalCount = entityList.LongCount();
            groups = deptGroups.Select(g => new AggregateGroupDto(
                _departments.GetValueOrDefault(g.Key, "未知部门"),
                g.LongCount(),
                totalCount > 0 ? (double)g.LongCount() / totalCount * 100 : 0)).ToList();
        }
        else
        {
            groups = new List<AggregateGroupDto>();
            totalCount = 0;
        }

        return new AggregateQueryResultDto(groups, totalCount);
    }

    /// <summary>
    /// 获取关系详情
    /// </summary>
    public RelationDetailDto? GetRelationDetail(string relationId)
    {
        if (!_relations.TryGetValue(relationId, out var relation))
            return null;

        return new RelationDetailDto(
            ToRelationDto(relation),
            ToDto(_entities[relation.SourceId]),
            ToDto(_entities[relation.TargetId]));
    }

    /// <summary>
    /// 更新关系
    /// </summary>
    public GraphRelationDto? UpdateRelation(string id, UpdateRelationRequestDto request)
    {
        if (!_relations.TryGetValue(id, out var relation))
            return null;

        if (!string.IsNullOrWhiteSpace(request.Type))
            relation.Type = request.Type;

        if (request.Properties is not null)
        {
            relation.Properties = JsonSerializer.SerializeToNode(request.Properties) as JsonObject ?? new JsonObject();
        }

        return ToRelationDto(relation);
    }

    private GraphPathDto BuildPathDto(List<(string EntityId, string RelationId, string Direction)> path)
    {
        var nodes = new List<GraphEntityDto>();
        var edges = new List<GraphRelationDto>();

        for (int i = 0; i < path.Count; i++)
        {
            nodes.Add(ToDto(_entities[path[i].EntityId]));

            if (i < path.Count - 1)
            {
                var rel = _relations[path[i + 1].RelationId];
                edges.Add(ToRelationDto(rel));
            }
        }

        return new GraphPathDto(nodes, edges, edges.Count);
    }

    private void AddEntity(GraphEntityRecord entity) => _entities[entity.Id] = entity;

    private void AddRelation(GraphRelationRecord relation) => _relations[relation.Id] = relation;

    private GraphEntityDto ToDto(GraphEntityRecord entity)
    {
        return new GraphEntityDto(
            entity.Id,
            entity.Name,
            entity.Type,
            entity.DepartmentId,
            _departments.GetValueOrDefault(entity.DepartmentId, "未知部门"),
            entity.IsPublic,
            entity.Properties.DeepClone() as JsonObject ?? new JsonObject(),
            entity.CreatedAt,
            entity.CreatedBy);
    }

    private GraphRelationDto ToRelationDto(GraphRelationRecord relation)
    {
        return new GraphRelationDto(
            relation.Id,
            relation.SourceId,
            _entities.TryGetValue(relation.SourceId, out var source) ? source.Name : relation.SourceId,
            relation.TargetId,
            _entities.TryGetValue(relation.TargetId, out var target) ? target.Name : relation.TargetId,
            relation.Type,
            relation.DepartmentId,
            relation.Properties.DeepClone() as JsonObject ?? new JsonObject(),
            relation.CreatedAt,
            relation.CreatedBy);
    }

    private sealed class GraphEntityRecord
    {
        public GraphEntityRecord(string id, string name, string type, Guid departmentId, bool isPublic, JsonObject properties, Guid createdBy, DateTime createdAt)
        {
            Id = id;
            Name = name;
            Type = type;
            DepartmentId = departmentId;
            IsPublic = isPublic;
            Properties = properties;
            CreatedBy = createdBy;
            CreatedAt = createdAt;
        }

        public string Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public Guid DepartmentId { get; set; }
        public bool IsPublic { get; set; }
        public JsonObject Properties { get; set; }
        public Guid CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    private sealed class GraphRelationRecord
    {
        public GraphRelationRecord(string id, string sourceId, string targetId, string type, Guid departmentId, JsonObject properties, Guid createdBy, DateTime createdAt)
        {
            Id = id;
            SourceId = sourceId;
            TargetId = targetId;
            Type = type;
            DepartmentId = departmentId;
            Properties = properties;
            CreatedBy = createdBy;
            CreatedAt = createdAt;
        }

        public string Id { get; set; }
        public string SourceId { get; set; }
        public string TargetId { get; set; }
        public string Type { get; set; }
        public Guid DepartmentId { get; set; }
        public JsonObject Properties { get; set; }
        public Guid CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// 获取图谱预览数据（用于可视化概览）
    /// </summary>
    public GraphPreviewResultDto GetPreview(int limit = 100)
    {
        limit = Math.Clamp(limit, 1, Math.Max(_entities.Count, 1));

        var entityDegrees = new Dictionary<string, int>();
        foreach (var entity in _entities.Keys)
        {
            entityDegrees[entity] = _relations.Values.Count(r => r.SourceId == entity || r.TargetId == entity);
        }

        var adjacency = _entities.Keys.ToDictionary(entityId => entityId, _ => new HashSet<string>());
        foreach (var relation in _relations.Values)
        {
            adjacency[relation.SourceId].Add(relation.TargetId);
            adjacency[relation.TargetId].Add(relation.SourceId);
        }

        var orderedEntityIds = _entities.Keys
            .OrderByDescending(entityId => entityDegrees.GetValueOrDefault(entityId, 0))
            .ThenBy(entityId => _entities[entityId].Name)
            .ToList();

        var selectedEntityIds = new List<string>(limit);
        var visited = new HashSet<string>();

        foreach (var seedId in orderedEntityIds)
        {
            if (selectedEntityIds.Count >= limit)
            {
                break;
            }

            if (!visited.Add(seedId))
            {
                continue;
            }

            var queue = new Queue<string>();
            queue.Enqueue(seedId);

            while (queue.Count > 0 && selectedEntityIds.Count < limit)
            {
                var currentId = queue.Dequeue();
                selectedEntityIds.Add(currentId);

                foreach (var neighborId in adjacency[currentId]
                    .OrderByDescending(entityId => entityDegrees.GetValueOrDefault(entityId, 0))
                    .ThenBy(entityId => _entities[entityId].Name))
                {
                    if (visited.Add(neighborId))
                    {
                        queue.Enqueue(neighborId);
                    }
                }
            }
        }

        foreach (var entityId in orderedEntityIds)
        {
            if (selectedEntityIds.Count >= limit)
            {
                break;
            }

            if (!selectedEntityIds.Contains(entityId))
            {
                selectedEntityIds.Add(entityId);
            }
        }

        var topEntityIds = new HashSet<string>(selectedEntityIds);

        var relevantRelations = _relations.Values
            .Where(r => topEntityIds.Contains(r.SourceId) && topEntityIds.Contains(r.TargetId))
            .ToList();

        var nodes = selectedEntityIds.Select(entityId => _entities[entityId]).Select(entity => new GraphPreviewNodeDto(
            entity.Id,
            entity.Name,
            entity.Type,
            _departments.GetValueOrDefault(entity.DepartmentId, "未知部门"),
            entity.IsPublic,
            entityDegrees.GetValueOrDefault(entity.Id, 0)
        )).ToList();

        var edges = relevantRelations.Select(r => new GraphPreviewEdgeDto(
            r.Id,
            r.SourceId,
            r.TargetId,
            r.Type
        )).ToList();

        return new GraphPreviewResultDto(nodes, edges, _entities.Count, _relations.Count);
    }
}
