using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QiaKon.Contracts;
using QiaKon.Contracts.DTOs;

namespace QiaKon.Shared;

internal sealed class PostgresGraphService : IGraphService
{
    private readonly QiaKonAppDbContext _dbContext;
    private readonly ILogger<PostgresGraphService>? _logger;

    public PostgresGraphService(QiaKonAppDbContext dbContext, ILogger<PostgresGraphService>? logger = null)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public EntityPagedResultDto GetEntities(string? label, int offset, int limit)
    {
        var query = _dbContext.GraphEntities.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(label))
        {
            query = query.Where(e => e.Type.Contains(label) || e.Name.Contains(label));
        }

        var totalCount = query.LongCount();
        var items = query.OrderBy(e => e.Name).Skip(offset).Take(limit).ToList().Select(ToDto).ToList();
        return new EntityPagedResultDto(items, totalCount, offset, limit);
    }

    public EntityPagedResultDto GetEntitiesFiltered(string? name, string? type, Guid? departmentId, bool? isPublic, int offset, int limit)
    {
        var query = _dbContext.GraphEntities.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(name))
        {
            query = query.Where(e => e.Name.Contains(name));
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(e => e.Type == type);
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
        var items = query.OrderBy(e => e.Name).Skip(offset).Take(limit).ToList().Select(ToDto).ToList();
        return new EntityPagedResultDto(items, totalCount, offset, limit);
    }

    public EntityDetailDto? GetEntity(string id)
    {
        var snapshot = LoadSnapshot();
        if (!snapshot.Entities.TryGetValue(id, out var entity))
        {
            return null;
        }

        var neighbors = snapshot.Relations.Values
            .Where(r => r.SourceId == id || r.TargetId == id)
            .Select(r => new NeighborDto(
                ToDto(snapshot.Entities[r.SourceId == id ? r.TargetId : r.SourceId]),
                r.Type,
                r.SourceId == id ? "outgoing" : "incoming"))
            .ToList();

        return new EntityDetailDto(ToDto(entity), neighbors, neighbors.Count);
    }

    public GraphEntityDto CreateEntity(CreateEntityRequestDto request, Guid userId)
    {
        var entity = new GraphEntityRow
        {
            Id = $"entity_{Guid.NewGuid():N}",
            Name = request.Name,
            Type = request.Type,
            DepartmentId = request.DepartmentId ?? QiaKonSeedData.GetDefaultDepartmentId(),
            IsPublic = (request.AccessLevel ?? AccessLevel.Department) == AccessLevel.Public,
            PropertiesJson = SerializeDictionary(request.Properties),
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow,
        };

        _dbContext.GraphEntities.Add(entity);
        _dbContext.SaveChanges();
        _logger?.LogInformation("Graph entity created in PostgreSQL: {EntityId} ({Name})", entity.Id, entity.Name);
        return ToDto(entity);
    }

    public GraphEntityDto? UpdateEntity(string id, UpdateEntityRequestDto request)
    {
        var entity = _dbContext.GraphEntities.FirstOrDefault(e => e.Id == id);
        if (entity is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            entity.Name = request.Name;
        }

        if (!string.IsNullOrWhiteSpace(request.Type))
        {
            entity.Type = request.Type;
        }

        if (request.Properties is not null)
        {
            entity.PropertiesJson = SerializeDictionary(request.Properties);
        }

        entity.UpdatedAt = DateTime.UtcNow;
        _dbContext.SaveChanges();
        return ToDto(entity);
    }

    public bool DeleteEntity(string id)
    {
        var entity = _dbContext.GraphEntities.FirstOrDefault(e => e.Id == id);
        if (entity is null)
        {
            return false;
        }

        var relations = _dbContext.GraphRelations.Where(r => r.SourceId == id || r.TargetId == id).ToList();
        if (relations.Count > 0)
        {
            _dbContext.GraphRelations.RemoveRange(relations);
        }

        _dbContext.GraphEntities.Remove(entity);
        _dbContext.SaveChanges();
        return true;
    }

    public RelationListResultDto GetRelations(int offset, int limit, string? type = null)
        => GetRelationsFiltered(type, null, null, offset, limit);

    public RelationListResultDto GetRelationsFiltered(string? type, string? sourceEntityId, string? targetEntityId, int offset, int limit)
    {
        var query = _dbContext.GraphRelations.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(r => r.Type.Contains(type));
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
        var items = query.OrderBy(r => r.Type).Skip(offset).Take(limit).ToList();
        var nameMap = LoadEntityNameMap(items.SelectMany(r => new[] { r.SourceId, r.TargetId }).Distinct());

        return new RelationListResultDto(items.Select(r => ToRelationDto(r, nameMap)).ToList(), totalCount);
    }

    public RelationDetailDto? GetRelationDetail(string id)
    {
        var relation = _dbContext.GraphRelations.AsNoTracking().FirstOrDefault(r => r.Id == id);
        if (relation is null)
        {
            return null;
        }

        var entities = _dbContext.GraphEntities.AsNoTracking()
            .Where(e => e.Id == relation.SourceId || e.Id == relation.TargetId)
            .ToDictionary(e => e.Id, e => e);

        if (!entities.TryGetValue(relation.SourceId, out var source) || !entities.TryGetValue(relation.TargetId, out var target))
        {
            return null;
        }

        var nameMap = entities.ToDictionary(x => x.Key, x => x.Value.Name);
        return new RelationDetailDto(ToRelationDto(relation, nameMap), ToDto(source), ToDto(target));
    }

    public GraphRelationDto CreateRelation(CreateRelationRequestDto request, Guid userId)
    {
        var departmentId = _dbContext.GraphEntities.AsNoTracking()
            .Where(e => e.Id == request.SourceId)
            .Select(e => (Guid?)e.DepartmentId)
            .FirstOrDefault() ?? Guid.Empty;

        var relation = new GraphRelationRow
        {
            Id = $"rel_{Guid.NewGuid():N}",
            SourceId = request.SourceId,
            TargetId = request.TargetId,
            Type = request.Type,
            DepartmentId = departmentId,
            PropertiesJson = SerializeDictionary(request.Properties),
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow,
        };

        _dbContext.GraphRelations.Add(relation);
        _dbContext.SaveChanges();

        var nameMap = LoadEntityNameMap(new[] { relation.SourceId, relation.TargetId });
        return ToRelationDto(relation, nameMap);
    }

    public GraphRelationDto? UpdateRelation(string id, UpdateRelationRequestDto request)
    {
        var relation = _dbContext.GraphRelations.FirstOrDefault(r => r.Id == id);
        if (relation is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(request.Type))
        {
            relation.Type = request.Type;
        }

        if (request.Properties is not null)
        {
            relation.PropertiesJson = SerializeDictionary(request.Properties);
        }

        _dbContext.SaveChanges();
        var nameMap = LoadEntityNameMap(new[] { relation.SourceId, relation.TargetId });
        return ToRelationDto(relation, nameMap);
    }

    public bool DeleteRelation(string id)
    {
        var relation = _dbContext.GraphRelations.FirstOrDefault(r => r.Id == id);
        if (relation is null)
        {
            return false;
        }

        _dbContext.GraphRelations.Remove(relation);
        _dbContext.SaveChanges();
        return true;
    }

    public GraphQueryResponseDto Query(GraphQueryRequestDto request)
    {
        var snapshot = LoadSnapshot();
        if (string.IsNullOrWhiteSpace(request.StartEntityId) || string.IsNullOrWhiteSpace(request.EndEntityId))
        {
            return new GraphQueryResponseDto(Array.Empty<GraphPathDto>());
        }

        if (!snapshot.Entities.ContainsKey(request.StartEntityId) || !snapshot.Entities.ContainsKey(request.EndEntityId))
        {
            return new GraphQueryResponseDto(Array.Empty<GraphPathDto>());
        }

        var paths = new List<GraphPathDto>();
        var direct = snapshot.Relations.Values
            .Where(r => r.SourceId == request.StartEntityId && r.TargetId == request.EndEntityId)
            .Where(r => string.IsNullOrWhiteSpace(request.RelationType) || r.Type.Equals(request.RelationType, StringComparison.OrdinalIgnoreCase))
            .Select(r => new GraphPathDto(
                new[] { ToDto(snapshot.Entities[r.SourceId]), ToDto(snapshot.Entities[r.TargetId]) },
                new[] { ToRelationDto(r, snapshot.EntityNames) },
                1))
            .ToList();

        paths.AddRange(direct);

        if (paths.Count == 0 && request.MaxHops >= 2)
        {
            var twoHop = from first in snapshot.Relations.Values
                         where first.SourceId == request.StartEntityId
                         from second in snapshot.Relations.Values
                         where second.SourceId == first.TargetId && second.TargetId == request.EndEntityId
                         where string.IsNullOrWhiteSpace(request.RelationType)
                               || first.Type.Equals(request.RelationType, StringComparison.OrdinalIgnoreCase)
                               || second.Type.Equals(request.RelationType, StringComparison.OrdinalIgnoreCase)
                         select new GraphPathDto(
                             new[] { ToDto(snapshot.Entities[first.SourceId]), ToDto(snapshot.Entities[first.TargetId]), ToDto(snapshot.Entities[second.TargetId]) },
                             new[] { ToRelationDto(first, snapshot.EntityNames), ToRelationDto(second, snapshot.EntityNames) },
                             2);

            paths.AddRange(twoHop);
        }

        return new GraphQueryResponseDto(paths);
    }

    public PathQueryResultDto FindPaths(string sourceId, string targetId, int maxPaths, int maxHops)
    {
        var snapshot = LoadSnapshot();
        if (!snapshot.Entities.ContainsKey(sourceId) || !snapshot.Entities.ContainsKey(targetId))
        {
            return new PathQueryResultDto(Array.Empty<GraphPathDto>(), 0);
        }

        var paths = new List<GraphPathDto>();
        var visited = new Dictionary<string, int>();
        var queue = new Queue<List<(string EntityId, string RelationId, string Direction)>>();

        queue.Enqueue(new List<(string, string, string)> { (sourceId, string.Empty, string.Empty) });
        visited[sourceId] = 0;

        while (queue.Count > 0 && paths.Count < maxPaths)
        {
            var currentPath = queue.Dequeue();
            var currentId = currentPath.Last().EntityId;
            var currentDepth = currentPath.Count - 1;

            if (currentDepth >= maxHops)
            {
                continue;
            }

            var outgoingRelations = snapshot.Relations.Values.Where(r => r.SourceId == currentId);
            var incomingRelations = snapshot.Relations.Values.Where(r => r.TargetId == currentId);

            foreach (var relation in outgoingRelations)
            {
                var newPath = new List<(string, string, string)>(currentPath) { (relation.TargetId, relation.Id, "outgoing") };
                if (relation.TargetId == targetId)
                {
                    var pathDto = BuildPathDto(newPath, snapshot);
                    if (pathDto.TotalHops <= maxHops)
                    {
                        paths.Add(pathDto);
                    }
                }
                else if (!visited.ContainsKey(relation.TargetId) || visited[relation.TargetId] > currentDepth + 1)
                {
                    visited[relation.TargetId] = currentDepth + 1;
                    queue.Enqueue(newPath);
                }
            }

            foreach (var relation in incomingRelations)
            {
                var newPath = new List<(string, string, string)>(currentPath) { (relation.SourceId, relation.Id, "incoming") };
                if (relation.SourceId == targetId)
                {
                    var pathDto = BuildPathDto(newPath, snapshot);
                    if (pathDto.TotalHops <= maxHops)
                    {
                        paths.Add(pathDto);
                    }
                }
                else if (!visited.ContainsKey(relation.SourceId) || visited[relation.SourceId] > currentDepth + 1)
                {
                    visited[relation.SourceId] = currentDepth + 1;
                    queue.Enqueue(newPath);
                }
            }
        }

        return new PathQueryResultDto(paths, paths.Count);
    }

    public MultiHopQueryResultDto MultiHopQuery(string startId, int maxHops, IReadOnlyList<string>? relationTypes = null)
    {
        var snapshot = LoadSnapshot();
        if (!snapshot.Entities.ContainsKey(startId))
        {
            return new MultiHopQueryResultDto(startId, Array.Empty<ReachableEntityDto>(), 0);
        }

        var reachableEntities = new Dictionary<string, ReachableEntityDto>();
        var visited = new Dictionary<string, int>();
        var queue = new Queue<(string EntityId, int Depth, List<string> PathRelations)>();

        queue.Enqueue((startId, 0, new List<string>()));
        visited[startId] = 0;

        while (queue.Count > 0)
        {
            var (currentId, depth, pathRelations) = queue.Dequeue();
            if (depth >= maxHops)
            {
                continue;
            }

            var outgoingRelations = snapshot.Relations.Values.Where(r => r.SourceId == currentId);
            var incomingRelations = snapshot.Relations.Values.Where(r => r.TargetId == currentId);

            foreach (var relation in outgoingRelations)
            {
                if (relationTypes is not null && relationTypes.Count > 0 && !relationTypes.Contains(relation.Type, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                var newDepth = depth + 1;
                var newPath = new List<string>(pathRelations) { relation.Type };
                if (!visited.ContainsKey(relation.TargetId) || visited[relation.TargetId] > newDepth)
                {
                    visited[relation.TargetId] = newDepth;
                    queue.Enqueue((relation.TargetId, newDepth, newPath));
                    reachableEntities[relation.TargetId] = new ReachableEntityDto(ToDto(snapshot.Entities[relation.TargetId]), newDepth, newPath);
                }
            }

            foreach (var relation in incomingRelations)
            {
                if (relationTypes is not null && relationTypes.Count > 0 && !relationTypes.Contains(relation.Type, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                var newDepth = depth + 1;
                var newPath = new List<string>(pathRelations) { relation.Type };
                if (!visited.ContainsKey(relation.SourceId) || visited[relation.SourceId] > newDepth)
                {
                    visited[relation.SourceId] = newDepth;
                    queue.Enqueue((relation.SourceId, newDepth, newPath));
                    reachableEntities[relation.SourceId] = new ReachableEntityDto(ToDto(snapshot.Entities[relation.SourceId]), newDepth, newPath);
                }
            }
        }

        var result = reachableEntities.Values.OrderBy(x => x.MinHops).ToList();
        return new MultiHopQueryResultDto(startId, result, result.Count);
    }

    public NeighborsQueryResultDto FindNeighbors(string entityId, string direction, int limit)
    {
        var snapshot = LoadSnapshot();
        if (!snapshot.Entities.ContainsKey(entityId))
        {
            return new NeighborsQueryResultDto(entityId, Array.Empty<NeighborDto>(), 0);
        }

        var neighbors = new List<NeighborDto>();
        if (direction is "outgoing" or "both")
        {
            neighbors.AddRange(snapshot.Relations.Values
                .Where(r => r.SourceId == entityId)
                .Select(r => new NeighborDto(ToDto(snapshot.Entities[r.TargetId]), r.Type, "outgoing")));
        }

        if (direction is "incoming" or "both")
        {
            neighbors.AddRange(snapshot.Relations.Values
                .Where(r => r.TargetId == entityId)
                .Select(r => new NeighborDto(ToDto(snapshot.Entities[r.SourceId]), r.Type, "incoming")));
        }

        var limited = neighbors.Take(limit).ToList();
        return new NeighborsQueryResultDto(entityId, limited, neighbors.Count);
    }

    public AggregateQueryResultDto AggregateQuery(string groupBy, AggregateFilterDto? filters = null)
    {
        var query = _dbContext.GraphEntities.AsNoTracking().AsEnumerable();

        if (filters is not null)
        {
            if (filters.EntityTypes is not null && filters.EntityTypes.Count > 0)
            {
                query = query.Where(e => filters.EntityTypes.Contains(e.Type, StringComparer.OrdinalIgnoreCase));
            }

            if (filters.DepartmentId.HasValue)
            {
                query = query.Where(e => e.DepartmentId == filters.DepartmentId.Value);
            }

            if (filters.IsPublic.HasValue)
            {
                query = query.Where(e => e.IsPublic == filters.IsPublic.Value);
            }
        }

        var materialized = query.ToList();
        var totalCount = materialized.LongCount();
        List<AggregateGroupDto> groups;

        if (groupBy.Equals("type", StringComparison.OrdinalIgnoreCase))
        {
            groups = materialized
                .GroupBy(e => e.Type)
                .Select(g => new AggregateGroupDto(g.Key, g.LongCount(), totalCount > 0 ? g.LongCount() * 100d / totalCount : 0))
                .ToList();
        }
        else if (groupBy.Equals("department", StringComparison.OrdinalIgnoreCase))
        {
            groups = materialized
                .GroupBy(e => e.DepartmentId)
                .Select(g => new AggregateGroupDto(ResolveDepartmentName(g.Key), g.LongCount(), totalCount > 0 ? g.LongCount() * 100d / totalCount : 0))
                .ToList();
        }
        else
        {
            groups = new List<AggregateGroupDto>();
        }

        return new AggregateQueryResultDto(groups, totalCount);
    }

    public GraphPreviewResultDto GetPreview(int limit = 100)
    {
        var snapshot = LoadSnapshot();
        limit = Math.Clamp(limit, 1, Math.Max(snapshot.Entities.Count, 1));
        var entityDegrees = snapshot.Entities.Keys.ToDictionary(
            key => key,
            key => snapshot.Relations.Values.Count(r => r.SourceId == key || r.TargetId == key));

        var adjacency = snapshot.Entities.Keys.ToDictionary(key => key, _ => new HashSet<string>());
        foreach (var relation in snapshot.Relations.Values)
        {
            adjacency[relation.SourceId].Add(relation.TargetId);
            adjacency[relation.TargetId].Add(relation.SourceId);
        }

        var orderedEntityIds = snapshot.Entities.Keys
            .OrderByDescending(id => entityDegrees.GetValueOrDefault(id, 0))
            .ThenBy(id => snapshot.Entities[id].Name)
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
                    .OrderByDescending(id => entityDegrees.GetValueOrDefault(id, 0))
                    .ThenBy(id => snapshot.Entities[id].Name))
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

        var topEntityIds = selectedEntityIds.ToHashSet();
        var edges = snapshot.Relations.Values
            .Where(r => topEntityIds.Contains(r.SourceId) && topEntityIds.Contains(r.TargetId))
            .Select(r => new GraphPreviewEdgeDto(r.Id, r.SourceId, r.TargetId, r.Type))
            .ToList();

        var nodes = selectedEntityIds
            .Select(id => snapshot.Entities[id])
            .Select(entity => new GraphPreviewNodeDto(
                entity.Id,
                entity.Name,
                entity.Type,
                ResolveDepartmentName(entity.DepartmentId),
                entity.IsPublic,
                entityDegrees.GetValueOrDefault(entity.Id, 0)))
            .ToList();

        return new GraphPreviewResultDto(nodes, edges, snapshot.Entities.Count, snapshot.Relations.Count);
    }

    private GraphSnapshot LoadSnapshot()
    {
        var entities = _dbContext.GraphEntities.AsNoTracking().ToList().ToDictionary(e => e.Id, e => e);
        var relations = _dbContext.GraphRelations.AsNoTracking().ToList().ToDictionary(r => r.Id, r => r);
        var entityNames = entities.ToDictionary(x => x.Key, x => x.Value.Name);
        return new GraphSnapshot(entities, relations, entityNames);
    }

    private Dictionary<string, string> LoadEntityNameMap(IEnumerable<string> ids)
    {
        var distinctIds = ids.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
        return _dbContext.GraphEntities.AsNoTracking()
            .Where(e => distinctIds.Contains(e.Id))
            .ToDictionary(e => e.Id, e => e.Name);
    }

    private GraphPathDto BuildPathDto(List<(string EntityId, string RelationId, string Direction)> path, GraphSnapshot snapshot)
    {
        var nodes = new List<GraphEntityDto>();
        var edges = new List<GraphRelationDto>();

        for (var i = 0; i < path.Count; i++)
        {
            nodes.Add(ToDto(snapshot.Entities[path[i].EntityId]));
            if (i < path.Count - 1)
            {
                var relation = snapshot.Relations[path[i + 1].RelationId];
                edges.Add(ToRelationDto(relation, snapshot.EntityNames));
            }
        }

        return new GraphPathDto(nodes, edges, edges.Count);
    }

    private GraphEntityDto ToDto(GraphEntityRow entity)
        => new(
            entity.Id,
            entity.Name,
            entity.Type,
            entity.DepartmentId,
            ResolveDepartmentName(entity.DepartmentId),
            entity.IsPublic,
            ParseJson(entity.PropertiesJson) ?? new JsonObject(),
            entity.CreatedAt,
            entity.CreatedBy);

    private GraphRelationDto ToRelationDto(GraphRelationRow relation, IReadOnlyDictionary<string, string> entityNames)
        => new(
            relation.Id,
            relation.SourceId,
            entityNames.TryGetValue(relation.SourceId, out var sourceName) ? sourceName : relation.SourceId,
            relation.TargetId,
            entityNames.TryGetValue(relation.TargetId, out var targetName) ? targetName : relation.TargetId,
            relation.Type,
            relation.DepartmentId,
            ParseJson(relation.PropertiesJson) ?? new JsonObject(),
            relation.CreatedAt,
            relation.CreatedBy);

    private static string SerializeDictionary(Dictionary<string, object?>? values)
        => values is null
            ? "{}"
            : JsonSerializer.Serialize(values, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private static JsonObject? ParseJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonNode.Parse(json) as JsonObject;
    }

    private string ResolveDepartmentName(Guid departmentId)
        => _dbContext.Departments.AsNoTracking()
            .Where(x => x.Id == departmentId)
            .Select(x => x.Name)
            .FirstOrDefault() ?? QiaKonSeedData.GetDepartmentName(departmentId);

    private sealed record GraphSnapshot(
        IReadOnlyDictionary<string, GraphEntityRow> Entities,
        IReadOnlyDictionary<string, GraphRelationRow> Relations,
        IReadOnlyDictionary<string, string> EntityNames);
}
