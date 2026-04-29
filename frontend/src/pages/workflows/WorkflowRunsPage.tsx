import { PageHeader } from '@/components/shared/PageHeader'
import { Card, CardContent } from '@/components/ui/Card'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { Play, Clock, AlertCircle } from 'lucide-react'
import { useState, useEffect, useCallback } from 'react'
import { workflowApi } from '@/services/api'
import type { WorkflowExecution } from '@/types'

export function WorkflowRunsPage() {
  const [executions, setExecutions] = useState<WorkflowExecution[]>([])
  const [loading, setLoading] = useState(false)

  const fetchExecutions = useCallback(async () => {
    setLoading(true)
    try {
      const data = await workflowApi.getExecutions(1, 50)
      setExecutions(data.items)
    } catch {
      // ignore
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    fetchExecutions()
    const timer = setInterval(fetchExecutions, 5000)
    return () => clearInterval(timer)
  }, [fetchExecutions])

  const formatDuration = (ms: number | null) => {
    if (!ms) return '-'
    if (ms < 1000) return `${ms}ms`
    return `${(ms / 1000).toFixed(1)}s`
  }

  const formatTime = (dateStr: string) => {
    return new Date(dateStr).toLocaleString('zh-CN')
  }

  const getStatusBadge = (status: string) => {
    switch (status.toLowerCase()) {
      case 'completed': return 'success'
      case 'failed': return 'error'
      case 'running': return 'warning'
      default: return 'default'
    }
  }

  return (
    <div className="mx-auto w-full max-w-7xl p-4 md:p-8">
      <PageHeader title="运行记录" description="查看工作流执行历史与结果" />

      {loading && executions.length === 0 ? (
        <div className="text-center py-12 text-gray-400">加载中...</div>
      ) : executions.length === 0 ? (
        <div className="text-center py-12 text-gray-400">
          <Play className="w-10 h-10 mx-auto mb-3 opacity-30" />
          <p>暂无执行记录</p>
        </div>
      ) : (
        <div className="space-y-3">
          {executions.map((exec) => (
            <Card key={exec.id} className="hover:shadow-md transition-shadow">
              <CardContent className="py-4">
                <div className="flex items-center gap-3 sm:gap-4">
                  <div className={`w-10 h-10 rounded-lg flex items-center justify-center flex-shrink-0 ${
                    exec.status === 'Completed' ? 'bg-green-50' : exec.status === 'Failed' ? 'bg-red-50' : 'bg-amber-50'
                  }`}>
                    {exec.status === 'Failed' ? (
                      <AlertCircle className="w-5 h-5 text-red-600" />
                    ) : (
                      <Play className={`w-5 h-5 ${exec.status === 'Completed' ? 'text-green-600' : 'text-amber-600'}`} />
                    )}
                  </div>
                  <div className="flex-1 min-w-0">
                    <p className="text-sm font-medium text-gray-900 truncate">{exec.pipelineName}</p>
                    <div className="flex items-center gap-3 mt-1">
                      <span className="text-xs text-gray-400 flex items-center gap-1">
                        <Clock className="w-3 h-3 flex-shrink-0" />
                        {formatTime(exec.startedAt)}
                      </span>
                      <span className="text-xs text-gray-400">耗时 {formatDuration(exec.duration)}</span>
                    </div>
                  </div>
                  <div className="flex items-center gap-3 flex-shrink-0">
                    <StatusBadge status={getStatusBadge(exec.status)} />
                  </div>
                </div>
                {exec.error && (
                  <p className="mt-2 text-xs text-red-600 bg-red-50 rounded px-2 py-1">{exec.error}</p>
                )}
              </CardContent>
            </Card>
          ))}
        </div>
      )}
    </div>
  )
}
