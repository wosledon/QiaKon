namespace QiaKon.Contracts.DTOs;

/// <summary>
/// Dashboard统计卡片
/// </summary>
public sealed record DashboardStatsDto(
    long TotalDocuments,
    long TotalEntities,
    long TodayQuestions,
    long ActiveUsers,
    IReadOnlyList<RecentDocumentDto> RecentDocuments,
    IReadOnlyList<RecentChatDto> RecentChats,
    IReadOnlyList<ComponentHealthDto> ComponentHealth);

/// <summary>
/// 最近文档DTO
/// </summary>
public sealed record RecentDocumentDto(
    Guid Id,
    string Title,
    DateTime CreatedAt);

/// <summary>
/// 最近问答DTO
/// </summary>
public sealed record RecentChatDto(
    Guid ConversationId,
    string Title,
    DateTime CreatedAt,
    int MessageCount);

/// <summary>
/// 组件健康状态DTO
/// </summary>
public sealed record ComponentHealthDto(
    string Name,
    string Status,
    string? Description,
    double? ResponseTime);
