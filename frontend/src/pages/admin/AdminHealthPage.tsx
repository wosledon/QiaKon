import { useEffect, useState, useCallback } from 'react'
import { PageHeader } from '@/components/shared/PageHeader'
import { Card, CardContent } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { Dialog } from '@/components/ui/Dialog'
import { healthApi } from '@/services/api'
import type { ComponentHealth } from '@/types'
import { RefreshCw, Activity, Server, Database, HardDrive, Layers } from 'lucide-react'

const iconMap: Record<string, React.ReactNode> = {
  'API 服务': <Server className="w-6 h-6" />,
  'LLM Provider': <Layers className="w-6 h-6" />,
  'PostgreSQL': <Database className="w-6 h-6" />,
  'Redis': <HardDrive className="w-6 h-6" />,
  'Kafka': <Activity className="w-6 h-6" />,
}

const statusColor = (status: ComponentHealth['status']) => {
  switch (status) {
    case 'healthy': return 'bg-emerald-500'
    case 'degraded': return 'bg-amber-500'
    case 'unhealthy': return 'bg-rose-500'
    default: return 'bg-gray-400'
  }
}

const statusText = (status: ComponentHealth['status']) => {
  switch (status) {
    case 'healthy': return '健康'
    case 'degraded': return '降级'
    case 'unhealthy': return '异常'
    default: return '未知'
  }
}

export function AdminHealthPage() {
  const [components, setComponents] = useState<ComponentHealth[]>([])
  const [loading, setLoading] = useState(false)
  const [selected, setSelected] = useState<ComponentHealth | null>(null)

  const fetchHealth = useCallback(async () => {
    setLoading(true)
    try {
      const data = await healthApi.overview()
      setComponents(data)
    } catch {
      // ignore
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    fetchHealth()
    const timer = setInterval(fetchHealth, 30000)
    return () => clearInterval(timer)
  }, [fetchHealth])

  return (
    <div className="mx-auto w-full max-w-7xl p-4 md:p-8">
      <PageHeader title="健康检查" description="查看系统各组件健康状态">
        <Button variant="ghost" size="sm" onClick={fetchHealth} isLoading={loading}>
          <RefreshCw className="w-4 h-4" />
        </Button>
      </PageHeader>

      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
        {!loading && components.length === 0 && (
          <Card className="sm:col-span-2 lg:col-span-3 border-dashed">
            <CardContent className="py-12 text-center">
              <Activity className="mx-auto mb-3 h-10 w-10 text-gray-300" />
              <p className="text-sm font-medium text-gray-600">暂未获取到健康数据</p>
              <p className="mt-1 text-xs text-gray-400">可点击右上角刷新，或稍后再次查看。</p>
            </CardContent>
          </Card>
        )}
        {components.map(c => (
          <div
            key={c.name}
            className="cursor-pointer hover:shadow-md transition-shadow"
            onClick={() => setSelected(c)}
          >
            <Card>
              <CardContent className="pt-6">
                <div className="flex items-start justify-between">
                  <div className="flex items-center gap-3">
                    <div className={`w-10 h-10 rounded-lg flex items-center justify-center ${statusColor(c.status)} text-white`}>
                      {iconMap[c.name] || <Activity className="w-6 h-6" />}
                    </div>
                    <div>
                      <p className="text-sm font-medium text-gray-900">{c.name}</p>
                      <p className="text-xs text-gray-500">{statusText(c.status)}</p>
                    </div>
                  </div>
                  <div className={`w-2.5 h-2.5 rounded-full ${statusColor(c.status)}`} />
                </div>
                {c.responseTime != null && (
                  <p className="mt-4 text-xs text-gray-400">响应时间: {c.responseTime}ms</p>
                )}
                {c.error && (
                  <p className="mt-2 text-xs text-rose-600 truncate">{c.error}</p>
                )}
              </CardContent>
            </Card>
        </div>
      ))}
      </div>

      <Dialog
        open={!!selected}
        onClose={() => setSelected(null)}
        title={`${selected?.name} - 详细信息`}
        footer={<Button variant="ghost" onClick={() => setSelected(null)}>关闭</Button>}
      >
        {selected && (
          <div className="space-y-3">
            <div className="flex items-center gap-2">
              <span className="text-sm text-gray-500">状态:</span>
              <span className={`text-sm font-medium ${selected.status === 'healthy' ? 'text-emerald-600' : selected.status === 'degraded' ? 'text-amber-600' : 'text-rose-600'}`}>
                {statusText(selected.status)}
              </span>
            </div>
            {selected.responseTime != null && (
              <div className="flex items-center gap-2">
                <span className="text-sm text-gray-500">响应时间:</span>
                <span className="text-sm text-gray-900">{selected.responseTime}ms</span>
              </div>
            )}
            {selected.error && (
              <div>
                <span className="text-sm text-gray-500">错误信息:</span>
                <p className="mt-1 text-sm text-rose-600 bg-rose-50 rounded-lg p-3">{selected.error}</p>
              </div>
            )}
            {selected.details && (
              <div>
                <span className="text-sm text-gray-500">详细数据:</span>
                <pre className="mt-1 text-xs text-gray-700 bg-gray-50 rounded-lg p-3 overflow-auto">{JSON.stringify(selected.details, null, 2)}</pre>
              </div>
            )}
          </div>
        )}
      </Dialog>
    </div>
  )
}
