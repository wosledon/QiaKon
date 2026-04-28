# QiaKon src - AGENTS.md

> **目录**: 后端源码根目录  
> **职责**: 汇总所有后端模块的开发规范  
> **包含模块**: 11 个核心模块

---

## 一、模块清单

| 模块                      | 路径                           | 职责          | AGENTS.md                                    |
| ------------------------- | ------------------------------ | ------------- | -------------------------------------------- |
| `QiaKon.Api`              | `src/QiaKon.Api/`              | HTTP API 层   | [查看](../src/QiaKon.Api/AGENTS.md)          |
| `QiaKon.Contracts`        | `src/QiaKon.Contracts/`        | 通用契约      | [查看](../src/QiaKon.Contracts/AGENTS.md)    |
| `QiaKon.Cache.Hybrid`     | `src/QiaKon.Cache.Hybrid/`     | 混合缓存      | [查看](../src/QiaKon.Cache.Hybrid/AGENTS.md) |
| `QiaKon.Cache.Memory`     | `src/QiaKon.Cache.Memory/`     | 内存缓存      | 继承 Hybrid 规范                             |
| `QiaKon.Cache.Redis`      | `src/QiaKon.Cache.Redis/`      | Redis 缓存    | 继承 Hybrid 规范                             |
| `QiaKon.Connector`        | `src/QiaKon.Connector/`        | 连接器抽象    | [查看](../src/QiaKon.Connector/AGENTS.md)    |
| `QiaKon.Connector.Http`   | `src/QiaKon.Connector.Http/`   | HTTP 连接器   | 继承 Connector 规范                          |
| `QiaKon.Connector.Npgsql` | `src/QiaKon.Connector.Npgsql/` | Npgsql 连接器 | 继承 Connector 规范                          |
| `QiaKon.Llm`              | `src/QiaKon.Llm/`              | LLM 核心      | [查看](../src/QiaKon.Llm/AGENTS.md)          |
| `QiaKon.Workflow`         | `src/QiaKon.Workflow/`         | 工作流引擎    | [查看](../src/QiaKon.Workflow/AGENTS.md)     |
| `QiaKon.Retrieval`        | `src/QiaKon.Retrieval/`        | 检索管道      | [查看](../src/QiaKon.Retrieval/AGENTS.md)    |
| `QiaKon.Graph.Engine`     | `src/QiaKon.Graph.Engine/`     | 图谱引擎      | [查看](../src/QiaKon.Graph.Engine/AGENTS.md) |
| `QiaKon.Queue`            | `src/QiaKon.Queue/`            | 消息队列      | [查看](../src/QiaKon.Queue/AGENTS.md)        |

---

## 二、依赖关系

```
QiaKon.Api (入口)
├── QiaKon.Llm.*
│   ├── QiaKon.Llm.Context
│   ├── QiaKon.Llm.Prompt
│   ├── QiaKon.Llm.Providers
│   └── QiaKon.Llm.Tokenization
├── QiaKon.Workflow
├── QiaKon.Retrieval.*
│   ├── QiaKon.Retrieval.DocumentProcessor
│   ├── QiaKon.Retrieval.Chunking
│   ├── QiaKon.Retrieval.Chunking.MoE
│   ├── QiaKon.Retrieval.Embedding
│   └── QiaKon.Retrieval.VectorStore.*
├── QiaKon.Graph.Engine.*
│   ├── QiaKon.Graph.Engine.Memory
│   └── QiaKon.Graph.Engine.Npgsql
├── QiaKon.Connector.*
├── QiaKon.Cache.*
├── QiaKon.Queue.*
├── QiaKon.EntityFrameworkCore.*
└── QiaKon.Contracts (所有模块依赖)
```

---

## 三、通用开发规范

### 3.1 命名规范

| 类型     | 规范                     | 示例                                 |
| -------- | ------------------------ | ------------------------------------ |
| 接口     | `I` 前缀 + PascalCase    | `IConnector`                         |
| 类       | PascalCase               | `HttpConnector`                      |
| 方法     | PascalCase               | `ExecuteAsync`                       |
| 参数     | camelCase                | `cancellationToken`                  |
| 常量     | PascalCase 或 UPPER_CASE | `MaxRetryCount` 或 `MAX_RETRY_COUNT` |
| 私有字段 | `_` 前缀 + camelCase     | `_httpClient`                        |

### 3.2 项目结构

每个模块遵循标准结构：

```
QiaKon.{ModuleName}/
├── bin/
├── obj/
├── {Module}.csproj
├── AGENTS.md
├── README.md (可选)
└── {具体实现文件}
```

### 3.3 依赖注入

```csharp
// ServiceCollectionExtensions.cs
public static class HttpConnectorServiceCollectionExtensions
{
    public static IServiceCollection AddHttpConnector(
        this IServiceCollection services,
        Action<HttpConnectorOptions> configure)
    {
        services.Configure(configure);
        services.AddSingleton<IHttpConnector, HttpConnector>();
        return services;
    }
}
```

### 3.4 配置验证

```csharp
public class HttpConnectorOptions
{
    public const string SectionName = "HttpConnector";
    
    [Required]
    public string BaseUrl { get; set; } = string.Empty;
    
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}
```

---

## 四、测试规范

### 4.1 测试项目结构

```
tests/
├── QiaKon.Api.Tests/
├── QiaKon.Connector.Tests/
├── QiaKon.Llm.Tests/
└── ...
```

### 4.2 测试框架

- **测试框架**: xUnit
- **Mock 框架**: Moq
- **断言库**: FluentAssertions
- **集成测试**: Testcontainers

### 4.3 测试命名

```csharp
[Fact]
public async Task ExecuteAsync_WithValidRequest_ReturnsSuccess()
{
    // Arrange
    // Act
    // Assert
}
```

---

## 五、构建与运行

### 5.1 常用命令

```bash
# 编译所有项目
dotnet build

# 运行 API
dotnet run --project src/QiaKon.Api

# 运行测试
dotnet test

# 代码格式化
dotnet format

# 发布
dotnet publish src/QiaKon.Api -c Release
```

### 5.2 配置文件

- `appsettings.json`: 默认配置
- `appsettings.Development.json`: 开发环境配置
- `appsettings.Production.json`: 生产环境配置

---

## 六、AI 协同工作

### 6.1 角色分配

| AI Agent 角色    | 负责模块                     |
| ---------------- | ---------------------------- |
| **架构师**       | 接口设计、模块边界划分       |
| **后端实现专家** | 具体实现、Bug 修复、性能优化 |
| **AI 工程师**    | LLM、Retrieval、Prompt 工程  |
| **数据库专家**   | EF Core、SQL 优化            |
| **测试专家**     | 单元测试、集成测试           |

### 6.2 协同流程

1. **架构师**设计接口契约
2. **实现专家**并行实现各模块
3. **测试专家**编写测试
4. **审查专家**代码审查
5. 审查通过后合并

---

## 七、注意事项

1. **模块化**: 每个模块独立编译、测试
2. **接口优先**: 先定义接口，再实现
3. **依赖注入**: 使用 DI 管理依赖
4. **配置验证**: 启动时验证配置
5. **日志规范**: Warning 及以上才记录

---

**最后更新**: 2026-04-28  
**维护者**: 全栈首席架构师 Agent
