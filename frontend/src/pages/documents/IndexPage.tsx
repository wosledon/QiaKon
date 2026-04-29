import { useState, useEffect, useCallback } from 'react'
import { Button } from '@/components/ui/Button'
import { Card, CardHeader, CardContent } from '@/components/ui/Card'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { StatCard } from '@/components/shared/StatCard'
import { PageHeader } from '@/components/shared/PageHeader'
import { documentApi } from '@/services/api'
import type { IndexQueueItem, IndexStats } from '@/types'
import {
  Layers,
  Clock,
  CheckCircle,
  AlertTriangle,
  RefreshCw,
  RotateCcw,
  Loader2,
  AlertCircle,
} from 'lucide-react'

const statusLabelMap: Record<string, string> = {
  pending: '待索引',
  indexing: '索引中',
  completed: '已完成',
  failed: '失败',
}

function formatDuration(seconds: number): string {
  if (seconds < 60) return `${seconds.toFixed(1)}s`
  if (seconds < 3600) return `${(seconds / 60).toFixed(1)}min`
  return `${(seconds / 3600).toFixed(1)}h`
}

export function IndexPage() {
  const [queue, setQueue] = useState<IndexQueueItem[]>([])
  const [stats, setStats] = useState<IndexStats | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState('')
  const [actionLoading, setActionLoading] = useState<string | null>(null)

  const loadData = useCallback(async () => {
    setIsLoading(true)
    setError('')
    try {
      const [queueData, statsData] = await Promise.all([
        documentApi.indexQueue(),
        documentApi.indexStats(),
      ])
      setQueue(queueData)
      setStats(statsData)
    } catch (err) {
      setError(err instanceof Error ? err.message : '加载失败')
    } finally {
      setIsLoading(false)
    }
  }, [])

  useEffect(() => {
    loadData()
    // Auto-refresh every 10 seconds
    const interval = setInterval(loadData, 10000)
    return () => clearInterval(interval)
  }, [loadData])

  const handleRetryFailed = async () => {
    setActionLoading('retry')
    try {
      await documentApi.retryFailed()
      await loadData()
    } catch (err) {
      setError(err instanceof Error ? err.message : '重试失败')
    } finally {
      setActionLoading(null)
    }
  }

  const handleRebuild = async () => {
    if (!confirm('确定要全量重建索引吗？这将重新索引所有文档，耗时可能较长。')) return
    setActionLoading('rebuild')
    try {
      await documentApi.rebuildIndex()
      await loadData()
    } catch (err) {
      setError(err instanceof Error ? err.message : '重建失败')
    } finally {
      setActionLoading(null)
    }
  }

  const pendingItems = queue.filter((q) => q.status === 'pending')
  const indexingItems = queue.filter((q) => q.status === 'indexing')
  const failedItems = queue.filter((q) => q.status === 'failed')

  return (
    <div className="p-4 md:p-8 max-w-6xl mx-auto">
      <PageHeader title="索引管理" description="查看索引队列、统计与批量操作">
        <div className="flex items-center gap-2">
          <Button
            variant="secondary"
            size="sm"
            onClick={handleRetryFailed}
            isLoading={actionLoading === 'retry'}
            disabled={failedItems.length === 0}
          >
            <RotateCcw className="w-4 h-4 mr-1" />
            重试失败
          </Button>
          <Button
            variant="secondary"
            size="sm"
            onClick={handleRebuild}
            isLoading={actionLoading === 'rebuild'}
          >
            <RefreshCw className="w-4 h-4 mr-1" />
            全量重建
          </Button>
        </div>
      </PageHeader>

      {/* Stats */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 mb-6">
        <StatCard
          title="总块数"
          value={stats?.totalChunks?.toLocaleString() ?? '-'}
          icon={<Layers className="w-5 h-5" />}
          color="blue"
        />
        <StatCard
          title="平均耗时"
          value={stats ? formatDuration(stats.avgDuration) : '-'}
          icon={<Clock className="w-5 h-5" />}
          color="amber"
        />
        <StatCard
          title="成功率"
          value={stats ? `${(stats.successRate * 100).toFixed(1)}%` : '-'}
          icon={<CheckCircle className="w-5 h-5" />}
          color="green"
        />
        <StatCard
          title="失败任务"
          value={stats?.failedCount?.toLocaleString() ?? '-'}
          icon={<AlertTriangle className="w-5 h-5" />}
          color="rose"
        />
      </div>

      {error && (
        <div className="mb-4 flex items-center gap-2 rounded-lg bg-red-50 px-4 py-3 text-sm text-red-600">
          <AlertCircle className="w-4 h-4" />
          {error}
        </div>
      )}

      {/* Indexing Queue */}
      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <h3 className="text-base font-semibold text-gray-900">索引队列</h3>
            <div className="flex items-center gap-4 text-xs text-gray-500">
              <span className="flex items-center gap-1">
                <span className="w-2 h-2 rounded-full bg-amber-400" />
                待索引 {pendingItems.length}
              </span>
              <span className="flex items-center gap-1">
                <span className="w-2 h-2 rounded-full bg-blue-400" />
                索引中 {indexingItems.length}
              </span>
              <span className="flex items-center gap-1">
                <span className="w-2 h-2 rounded-full bg-rose-400" />
                失败 {failedItems.length}
              </span>
            </div>
          </div>
        </CardHeader>
        <CardContent>
          {isLoading ? (
            <div className="flex items-center justify-center py-12 text-gray-400">
              <Loader2 className="w-6 h-6 animate-spin mr-2" />
              加载中...
            </div>
          ) : queue.length === 0 ? (
            <div className="text-center py-12 text-gray-400">
              <Layers className="w-10 h-10 mx-auto mb-2 text-gray-300" />
              <p>索引队列为空</p>
            </div>
          ) : (
            <div className="space-y-3">
              {queue.map((item) => (
                <div
                  key={item.documentId}
                  className="flex items-center gap-4 py-3 border-b border-gray-50 last:border-0"
                >
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2">
                      <p className="text-sm font-medium text-gray-900 truncate">
                        {item.title}
                      </p>
                      <StatusBadge status={item.status}>
                        {statusLabelMap[item.status] || item.status}
                      </StatusBadge>
                    </div>
                    <div className="mt-2 w-full bg-gray-100 rounded-full h-1.5">
                      <div
                        className={`h-1.5 rounded-full transition-all ${
                          item.status === 'failed'
                            ? 'bg-rose-500'
                            : item.status === 'completed'
                            ? 'bg-emerald-500'
                            : 'bg-blue-500'
                        }`}
                        style={{ width: `${item.progress}%` }}
                      />
                    </div>
                    <div className="flex items-center justify-between mt-1">
                      <span className="text-xs text-gray-400">{item.progress}%</span>
                      {item.errorMessage && (
                        <span className="text-xs text-red-500 truncate max-w-[60%]">
                          {item.errorMessage}
                        </span>
                      )}
                    </div>
                  </div>
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  )
}
