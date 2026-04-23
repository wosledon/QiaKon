using Microsoft.Extensions.Hosting;

namespace QiaKon.Connector;

/// <summary>
/// 连接器宿主服务，负责初始化和关闭所有连接器
/// </summary>
public sealed class ConnectorHostedService : BackgroundService
{
    private readonly ConnectorManager _manager;

    public ConnectorHostedService(ConnectorManager manager)
    {
        _manager = manager;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _manager.InitializeAsync(stoppingToken);

        // 等待宿主停止
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _manager.CloseAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}
