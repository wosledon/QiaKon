# QiaKon.Api - AGENTS.md

> **模块**: HTTP API 层  
> **职责**: 路由、中间件、认证授权、请求处理  
> **依赖**: 所有业务模块  
> **被依赖**: 前端应用、第三方集成

---

## 一、模块职责

本模块是平台对外的 HTTP API 入口，负责接收用户请求、认证授权、路由分发、响应处理。

**核心职责**:
- RESTful API 路由定义
- JWT 认证与授权
- 请求验证与模型绑定
- 异常处理与错误响应
- 日志与审计
- CORS 与安全防护

---

## 二、API 架构

### 2.1 路由结构

```
/api
├── /auth              # 认证授权
│   ├── POST /login
│   ├── POST /refresh
│   └── POST /logout
├── /users             # 用户管理
│   ├── GET    /       # 用户列表
│   ├── POST   /       # 创建用户
│   ├── GET    /{id}   # 用户详情
│   ├── PUT    /{id}   # 更新用户
│   └── DELETE /{id}   # 删除用户
├── /documents         # 文档管理
│   ├── GET    /       # 文档列表
│   ├── POST   /       # 上传文档
│   ├── GET    /{id}   # 文档详情
│   ├── PUT    /{id}   # 更新文档
│   ├── DELETE /{id}   # 删除文档
│   └── POST   /{id}/index  # 触发索引
├── /graphs            # 知识图谱
│   ├── /entities      # 实体管理
│   ├── /relations     # 关系管理
│   └── /query         # 图查询
├── /retrieval         # 检索问答
│   ├── POST /search   # 检索
│   └── POST /chat     # 对话
├── /workflows         # 工作流
│   ├── GET    /       # 工作流列表
│   ├── POST   /       # 创建工作流
│   └── POST   /{id}/execute  # 执行工作流
└── /admin             # 系统管理
    ├── /departments   # 部门管理
    ├── /roles         # 角色管理
    └── /audit-logs    # 审计日志
```

### 2.2 控制器结构

```
Controllers/
├── AuthController.cs
├── UsersController.cs
├── DocumentsController.cs
├── GraphsController.cs
│   ├── EntitiesController.cs
│   └── RelationsController.cs
├── RetrievalController.cs
├── WorkflowsController.cs
└── AdminController.cs
    ├── DepartmentsController.cs
    ├── RolesController.cs
    └── AuditLogsController.cs
```

---

## 三、核心中间件

### 3.1 中间件管道

```
请求 → ExceptionHandler → Cors → Routing → Authentication → Authorization 
     → RateLimiting → RequestValidation → Endpoint → Response
```

### 3.2 认证中间件

```csharp
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = configuration["Jwt:Issuer"],
            ValidAudience = configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(configuration["Jwt:Key"]))
        };
    });
```

### 3.3 权限中间件

```csharp
public class PermissionMiddleware
{
    private readonly RequestDelegate _next;
    
    public async Task InvokeAsync(HttpContext context, IPermissionService permission)
    {
        var userId = context.User.GetUserId();
        var resource = context.GetResourceFromRoute();
        var action = context.Request.Method.ToAction();
        
        var hasPermission = await permission.CheckAsync(userId, resource, action);
        if (!hasPermission)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
        
        await _next(context);
    }
}
```

---

## 四、请求与响应

### 4.1 统一响应格式

```csharp
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public ApiError? Error { get; set; }
    public string? TraceId { get; set; }
}

public class ApiError
{
    public string Code { get; set; }
    public string Message { get; set; }
    public IDictionary<string, string[]>? ValidationErrors { get; set; }
}
```

### 4.2 请求验证

```csharp
public class CreateDocumentRequest
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; }
    
    [Required]
    public IFormFile File { get; set; }
    
    [Required]
    public Guid DepartmentId { get; set; }
    
    public bool IsPublic { get; set; }
    
    [EnumDataType(typeof(AccessLevel))]
    public AccessLevel AccessLevel { get; set; }
}
```

### 4.3 分页响应

```csharp
public class PagedResponse<T>
{
    public IReadOnlyList<T> Items { get; set; }
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}
```

---

## 五、错误处理

### 5.1 全局异常处理

```csharp
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        
        context.Response.StatusCode = exception switch
        {
            ValidationException => StatusCodes.Status400BadRequest,
            UnauthorizedException => StatusCodes.Status401Unauthorized,
            ForbiddenException => StatusCodes.Status403Forbidden,
            NotFoundException => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status500InternalServerError
        };
        
        await context.Response.WriteAsJsonAsync(new ApiResponse<object>
        {
            Success = false,
            Error = new ApiError
            {
                Code = "INTERNAL_ERROR",
                Message = exception.Message
            },
            TraceId = context.TraceIdentifier
        });
    });
});
```

### 5.2 业务异常

| 异常类型                | HTTP 状态码 | 说明         |
| ----------------------- | ----------- | ------------ |
| `ValidationException`   | 400         | 请求验证失败 |
| `UnauthorizedException` | 401         | 未认证       |
| `ForbiddenException`    | 403         | 无权限       |
| `NotFoundException`     | 404         | 资源不存在   |
| `ConflictException`     | 409         | 资源冲突     |

---

## 六、安全配置

### 6.1 CORS 配置

```csharp
services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(configuration["Cors:AllowedOrigins"])
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});
```

### 6.2 速率限制

```csharp
services.AddRateLimiter(options =>
{
    options.AddPolicy("ApiRateLimit", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User.GetUserId(),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1)
            }));
});
```

---

## 七、日志与审计

### 7.1 日志配置

```csharp
builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));

// 应用日志：Warning 及以上
builder.Logging.AddFilter("Default", LogLevel.Warning);

// 审计日志：Info 级别
builder.Logging.AddFilter("Audit", LogLevel.Information);
```

### 7.2 审计中间件

```csharp
public class AuditMiddleware
{
    public async Task InvokeAsync(HttpContext context, IAuditService audit)
    {
        var userId = context.User.GetUserId();
        var action = $"{context.Request.Method} {context.Request.Path}";
        var resource = context.GetResourceFromRoute();
        
        await audit.LogAsync(new AuditRecord
        {
            UserId = userId,
            Action = action,
            ResourceType = resource.Type,
            ResourceId = resource.Id,
            Timestamp = DateTime.UtcNow,
            IpAddress = context.Connection.RemoteIpAddress?.ToString()
        });
        
        await _next(context);
    }
}
```

---

## 八、开发规范

### 8.1 控制器规范

- 使用属性路由 `[Route("api/[controller]")]`
- 使用 `[ApiController]` 启用自动验证
- 使用 `[Authorize]` 保护需要认证的端点
- 使用 `[ProducesResponseType]` 文档化响应

### 8.2 服务注入

```csharp
public class DocumentsController : ControllerBase
{
    private readonly IDocumentService _documentService;
    private readonly ILogger<DocumentsController> _logger;
    
    public DocumentsController(
        IDocumentService documentService,
        ILogger<DocumentsController> logger)
    {
        _documentService = documentService;
        _logger = logger;
    }
}
```

### 8.3 异步编程

- 所有 I/O 操作使用异步
- 使用 `CancellationToken` 支持请求取消
- 避免 `.Result` 或 `.Wait()`

---

## 九、测试要求

### 9.1 单元测试

- 控制器动作逻辑
- 验证逻辑
- 权限检查逻辑

### 9.2 集成测试

- API 端到端测试
- 认证授权流程
- 错误处理逻辑

---

## 十、注意事项

1. **模型绑定**: 验证所有用户输入
2. **SQL 注入**: 使用参数化查询
3. **XSS 防护**: 响应内容转义
4. **CSRF 防护**: 使用 Anti-Forgery Token
5. **敏感数据**: 不在日志中记录密码、Token 等

---

**最后更新**: 2026-04-28  
**维护者**: 后端实现专家 Agent
