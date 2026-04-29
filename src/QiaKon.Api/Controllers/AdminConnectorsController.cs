using Microsoft.AspNetCore.Mvc;
using QiaKon.Contracts.DTOs;
using QiaKon.Shared;

namespace QiaKon.Api.Controllers;

[ApiController]
[Route("api/admin/connectors")]
public class AdminConnectorsController : ControllerBase
{
    private readonly IConnectorService _connectorService;

    public AdminConnectorsController(IConnectorService connectorService)
    {
        _connectorService = connectorService;
    }

    /// <summary>
    /// 获取所有连接器
    /// </summary>
    [HttpGet]
    public ApiResponse<IReadOnlyList<ConnectorDto>> GetAll()
    {
        var connectors = _connectorService.GetAll();
        return ApiResponse<IReadOnlyList<ConnectorDto>>.Ok(connectors);
    }

    /// <summary>
    /// 获取连接器详情
    /// </summary>
    [HttpGet("{id:guid}")]
    public ApiResponse<ConnectorDto> GetById(Guid id)
    {
        var connector = _connectorService.GetById(id);
        return connector is null
            ? ApiResponse<ConnectorDto>.Fail("连接器不存在", 404)
            : ApiResponse<ConnectorDto>.Ok(connector);
    }

    /// <summary>
    /// 创建连接器
    /// </summary>
    [HttpPost]
    public ApiResponse<ConnectorDto> Create([FromBody] CreateConnectorDto request)
    {
        var connector = _connectorService.Create(request);
        return ApiResponse<ConnectorDto>.Ok(connector, "连接器创建成功");
    }

    /// <summary>
    /// 更新连接器
    /// </summary>
    [HttpPut("{id:guid}")]
    public ApiResponse<ConnectorDto> Update(Guid id, [FromBody] CreateConnectorDto request)
    {
        var connector = _connectorService.Update(id, request);
        return connector is null
            ? ApiResponse<ConnectorDto>.Fail("连接器不存在", 404)
            : ApiResponse<ConnectorDto>.Ok(connector, "连接器更新成功");
    }

    /// <summary>
    /// 删除连接器
    /// </summary>
    [HttpDelete("{id:guid}")]
    public ApiResponse Delete(Guid id)
    {
        var result = _connectorService.Delete(id);
        return result
            ? ApiResponse.Ok("连接器删除成功")
            : ApiResponse.Fail("连接器不存在", 404);
    }

    /// <summary>
    /// 健康检查
    /// </summary>
    [HttpPost("{id:guid}/health")]
    public ApiResponse<ConnectorHealthResultDto> CheckHealth(Guid id)
    {
        var result = _connectorService.CheckHealth(id);
        return ApiResponse<ConnectorHealthResultDto>.Ok(result);
    }
}
