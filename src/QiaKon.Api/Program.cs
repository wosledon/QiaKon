using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using QiaKon.Api.Middleware;
using QiaKon.Cache;
using QiaKon.Cache.Hybrid;
using QiaKon.Cache.Memory;
using QiaKon.Connector;
using QiaKon.Connector.Http;
using QiaKon.Connector.Npgsql;
using QiaKon.EntityFrameworkCore.Npgsql;
using QiaKon.Graph.Engine.Memory;
using QiaKon.Graph.Engine.Npgsql;
using QiaKon.Llm;
using QiaKon.Llm.Context;
using QiaKon.Llm.Prompt;
using QiaKon.Llm.Providers;
using QiaKon.Llm.Tokenization;
using QiaKon.Queue;
using QiaKon.Queue.Kafka;
using QiaKon.Queue.Memory;
using QiaKon.Retrieval.Chunnking;
using QiaKon.Retrieval.DocumentProcessor;
using QiaKon.Retrieval.Embedding;
using QiaKon.Retrieval.VectorStore.Npgsql;
using QiaKon.Workflow;
using QiaKon.Workflow.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(8080);
});

// Add services
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Configuration
var connectionString = builder.Configuration.GetConnectionString("Default")!;
var redisConnectionString = builder.Configuration.GetConnectionString("Redis")!;
var isDevelopment = builder.Environment.IsDevelopment();

// ============ Cache Services ============
if (isDevelopment)
{
    // 开发环境使用内存缓存
    builder.Services.AddMemoryCache();
    builder.Services.AddSingleton<ICache, MemoryCache>();
}
else
{
    // 生产环境使用混合缓存
    builder.Services.AddHybridCache(redisConnectionString);
}

// ============ Connector Services ============
// Http Connector
builder.Services.AddHttpConnectorSupport();
// Npgsql Connector
builder.Services.AddNpgsqlConnectorSupport();

// ============ LLM Services ============
// Tokenizer
builder.Services.AddLlmTokenizer("default");

// Context
builder.Services.AddConversationContext(maxMessages: 50, maxTokens: 8000);

// Prompt
builder.Services.AddSingleton<PromptTemplate>();

// LLM Client Factory
builder.Services.AddSingleton<ILlmClientFactory, LlmClientFactory>();

// ============ Workflow Services ============
builder.Services.AddWorkflowCore();

// ============ Retrieval Services ============
// Chunking (开发环境使用简单字符分块)
builder.Services.AddCharacterChunking();

// Embedding
builder.Services.AddLocalEmbedding(options =>
{
    options.ModelPath = "./models/embedding";
    options.Dimensions = 384;
});

// VectorStore
builder.Services.AddNpgsqlVectorStore(options =>
{
    options.ConnectionString = connectionString;
});

// DocumentProcessor
builder.Services.AddMarkItDownDocumentProcessor();

// ============ Graph Engine Services ============
if (isDevelopment)
{
    // 开发环境使用内存图引擎
    builder.Services.AddMemoryGraphEngine();
}
else
{
    // 生产环境使用 Npgsql 图引擎
    builder.Services.AddNpgsqlGraphEngine(options =>
    {
        options.ConnectionString = connectionString;
    });
}

// ============ Queue Services ============
if (isDevelopment)
{
    // 开发环境使用内存队列
    builder.Services.AddSingleton<IQueue, MemoryQueue>();
}
else
{
    // 生产环境使用 Kafka
    builder.Services.AddKafkaQueue(options =>
    {
        options.BootstrapServers = "127.0.0.1:9092";
    });
}

// ============ EF Core DbContext ============
builder.Services.AddQiaKonNpgsqlDbContext<QiaKonNpgsqlDbContext>(connectionString);

// ============ JWT Authentication ============
var jwtSection = builder.Configuration.GetSection("Jwt");
var secretKey = jwtSection["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey is not configured");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
        };
    });

builder.Services.AddAuthorization();

// ============ CORS ============
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// ============ Health Checks ============
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "postgresql")
    .AddRedis(redisConnectionString, name: "redis");

var app = builder.Build();

// Middleware pipeline
app.UseExceptionHandling();
app.UseAuditLogging();

app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();

// Health check endpoint
app.MapHealthChecks("/health");

app.MapControllers();

app.Run();
