import { useEffect, useState, useCallback } from 'react'
import { PageHeader } from '@/components/shared/PageHeader'
import { Card, CardContent } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { auditApi } from '@/services/api'
import type { AuditLog } from '@/types'
import { ChevronDown, ChevronUp, Search } from 'lucide-react'

export function AdminAuditPage() {
  const [logs, setLogs] = useState<AuditLog[]>([])
  const [total, setTotal] = useState(0)
  const [page, setPage] = useState(1)
  const [pageSize] = useState(20)
  const [loading, setLoading] = useState(false)
  const [expanded, setExpanded] = useState<Set<string>>(new Set())

  const [filters, setFilters] = useState({
    operationType: '',
    username: '',
    startDate: '',
    endDate: '',
  })

  const fetchLogs = useCallback(async () => {
    setLoading(true)
    try {
      const res = await auditApi.logs(page, pageSize, {
        operationType: filters.operationType || undefined,
        username: filters.username || undefined,
        startDate: filters.startDate || undefined,
        endDate: filters.endDate || undefined,
      })
      setLogs(res.items)
      setTotal(res.totalCount)
    } catch {
      // ignore
    } finally {
      setLoading(false)
    }
  }, [page, pageSize, filters])

  useEffect(() => {
    fetchLogs()
  }, [fetchLogs])

  const toggleExpand = (id: string) => {
    setExpanded(prev => {
      const next = new Set(prev)
      if (next.has(id)) next.delete(id)
      else next.add(id)
      return next
    })
  }

  const totalPages = Math.ceil(total / pageSize)

  return (
    <div className="mx-auto w-full max-w-7xl p-4 md:p-8">
      <PageHeader title="审计日志" description="查看系统操作审计记录" />

      <Card className="mb-4">
        <CardContent className="py-4">
          <div className="flex flex-wrap gap-3">
            <div className="flex-1 min-w-[160px]">
              <label className="block text-xs text-gray-500 mb-1">操作类型</label>
              <select
                className="w-full px-3 py-2 rounded-lg border border-gray-300 bg-white text-sm text-gray-900 focus:outline-none focus:ring-2 focus:ring-blue-500"
                value={filters.operationType}
                onChange={e => setFilters(prev => ({ ...prev, operationType: e.target.value }))}
              >
                <option value="">全部</option>
                <option value="create">创建</option>
                <option value="update">更新</option>
                <option value="delete">删除</option>
                <option value="login">登录</option>
                <option value="logout">登出</option>
              </select>
            </div>
            <div className="flex-1 min-w-[160px]">
              <label className="block text-xs text-gray-500 mb-1">用户</label>
              <div className="relative">
                <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
                <input
                  className="w-full pl-9 pr-3 py-2 rounded-lg border border-gray-300 bg-white text-sm text-gray-900 focus:outline-none focus:ring-2 focus:ring-blue-500"
                  placeholder="搜索用户..."
                  value={filters.username}
                  onChange={e => setFilters(prev => ({ ...prev, username: e.target.value }))}
                />
              </div>
            </div>
            <div className="flex-1 min-w-[160px]">
              <label className="block text-xs text-gray-500 mb-1">开始日期</label>
              <input
                type="date"
                className="w-full px-3 py-2 rounded-lg border border-gray-300 bg-white text-sm text-gray-900 focus:outline-none focus:ring-2 focus:ring-blue-500"
                value={filters.startDate}
                onChange={e => setFilters(prev => ({ ...prev, startDate: e.target.value }))}
              />
            </div>
            <div className="flex-1 min-w-[160px]">
              <label className="block text-xs text-gray-500 mb-1">结束日期</label>
              <input
                type="date"
                className="w-full px-3 py-2 rounded-lg border border-gray-300 bg-white text-sm text-gray-900 focus:outline-none focus:ring-2 focus:ring-blue-500"
                value={filters.endDate}
                onChange={e => setFilters(prev => ({ ...prev, endDate: e.target.value }))}
              />
            </div>
            <div className="flex items-end">
              <Button size="sm" onClick={() => { setPage(1); fetchLogs(); }}>
                筛选
              </Button>
            </div>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardContent className="py-4">
          {logs.length === 0 && !loading && (
            <p className="text-sm text-gray-400 text-center py-8">暂无审计日志</p>
          )}
          <div className="space-y-2 overflow-hidden">
            {logs.map(log => (
              <div key={log.id} className="border border-gray-100 rounded-lg overflow-hidden">
                <div
                  className="flex items-start gap-3 px-4 py-3 cursor-pointer hover:bg-gray-50 overflow-x-auto"
                  onClick={() => toggleExpand(log.id)}
                >
                  {expanded.has(log.id) ? (
                    <ChevronUp className="w-4 h-4 text-gray-400" />
                  ) : (
                    <ChevronDown className="w-4 h-4 text-gray-400" />
                  )}
                  <div className="flex-1 min-w-[720px] grid grid-cols-12 gap-2 items-center">
                    <span className="col-span-2 text-sm text-gray-900 truncate">{log.username}</span>
                    <span className="col-span-2 text-xs text-gray-500">{log.operationType}</span>
                    <span className="col-span-2 text-xs text-gray-500 truncate">{log.resourceType}</span>
                    <span className="col-span-3 text-xs text-gray-400 truncate">{log.resourceId}</span>
                    <span className="col-span-2 text-xs text-gray-400">{new Date(log.createdAt).toLocaleString()}</span>
                    <span className="col-span-1">
                      {log.result === 'success' ? (
                        <span className="text-xs text-emerald-600">成功</span>
                      ) : (
                        <span className="text-xs text-rose-600">失败</span>
                      )}
                    </span>
                  </div>
                </div>
                {expanded.has(log.id) && (
                  <div className="px-4 py-3 bg-gray-50 border-t border-gray-100 space-y-2">
                    {log.ipAddress && (
                      <p className="text-xs text-gray-500">IP 地址: {log.ipAddress}</p>
                    )}
                    {log.beforeValue && (
                      <div>
                        <p className="text-xs text-gray-500">变更前:</p>
                        <pre className="mt-1 text-xs text-gray-700 bg-white rounded-lg p-2 overflow-auto">{log.beforeValue}</pre>
                      </div>
                    )}
                    {log.afterValue && (
                      <div>
                        <p className="text-xs text-gray-500">变更后:</p>
                        <pre className="mt-1 text-xs text-gray-700 bg-white rounded-lg p-2 overflow-auto">{log.afterValue}</pre>
                      </div>
                    )}
                    {log.details && (
                      <div>
                        <p className="text-xs text-gray-500">详情:</p>
                        <pre className="mt-1 text-xs text-gray-700 bg-white rounded-lg p-2 overflow-auto">{log.details}</pre>
                      </div>
                    )}
                  </div>
                )}
              </div>
            ))}
          </div>

          {totalPages > 1 && (
            <div className="flex items-center justify-between mt-4 pt-4 border-t border-gray-100">
              <p className="text-xs text-gray-400">
                共 {total} 条，第 {page} / {totalPages} 页
              </p>
              <div className="flex gap-2">
                <Button variant="ghost" size="sm" disabled={page <= 1} onClick={() => setPage(p => p - 1)}>
                  上一页
                </Button>
                <Button variant="ghost" size="sm" disabled={page >= totalPages} onClick={() => setPage(p => p + 1)}>
                  下一页
                </Button>
              </div>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  )
}
