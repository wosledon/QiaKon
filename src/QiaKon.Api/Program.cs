using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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
using QiaKon.Shared;
using QiaKon.Workflow;
using QiaKon.Workflow.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(8080);
});

// ============ JSON Serialization ============
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    });

builder.Services.AddOpenApi();

// Configuration
var connectionString = builder.Configuration.GetConnectionString("Default") ?? "Host=localhost;Database=qiakon;Username=postgres;Password=postgres";
var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
var isDevelopment = builder.Environment.IsDevelopment();

// ============ Shared Services (PostgreSQL-backed core business data) ============
builder.Services.AddSharedServicesWithPostgres(connectionString);

// ============ Cache Services ============
if (isDevelopment)
{
    builder.Services.AddMemoryCache();
    builder.Services.AddSingleton<ICache, MemoryCache>();
}
else
{
    builder.Services.AddHybridCache(redisConnectionString);
}

// ============ Connector Services ============
builder.Services.AddHttpConnectorSupport();
builder.Services.AddNpgsqlConnectorSupport();

// ============ LLM Services ============
builder.Services.AddLlmTokenizer("default");
builder.Services.AddConversationContext(maxMessages: 50, maxTokens: 8000);
// 注：当前使用内存RAG服务，暂不注册PromptTemplate（需要模板字符串参数）
// builder.Services.AddSingleton<PromptTemplate>();
builder.Services.AddSingleton<ILlmClientFactory, LlmClientFactory>();

// ============ Workflow Services ============
builder.Services.AddWorkflowCore();

// ============ Retrieval Services ============
builder.Services.AddCharacterChunking();
builder.Services.AddLocalEmbedding(options =>
{
    options.ModelPath = "./models/embedding";
    options.Dimensions = 384;
});
builder.Services.AddNpgsqlVectorStore(options =>
{
    options.ConnectionString = connectionString;
});
builder.Services.AddMarkItDownDocumentProcessor();

// ============ Graph Engine Services ============
if (isDevelopment)
{
    builder.Services.AddMemoryGraphEngine();
}
else
{
    builder.Services.AddNpgsqlGraphEngine(options =>
    {
        options.ConnectionString = connectionString;
    });
}

// ============ Queue Services ============
if (isDevelopment)
{
    builder.Services.AddMemoryQueue();
}
else
{
    builder.Services.AddKafkaQueue(options =>
    {
        options.BootstrapServers = "127.0.0.1:9092";
    });
}

// ============ JWT Authentication ============
var jwtSection = builder.Configuration.GetSection("Jwt");
var secretKey = jwtSection["SecretKey"] ?? "QiaKon-Dev-Secret-Key-For-Development-Only-Min-32-Chars!";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"] ?? "QiaKon",
            ValidAudience = jwtSection["Audience"] ?? "QiaKon.Api",
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
    .AddNpgSql(connectionString, name: "postgresql", tags: new[] { "db" })
    .AddRedis(redisConnectionString, name: "redis", tags: new[] { "cache" });

var app = builder.Build();

await app.Services.InitializeQiaKonDatabaseAsync();

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

app.MapHealthChecks("/health");

app.MapControllers();

app.Run();
