import { useEffect, useState } from 'react'
import { PageHeader } from '@/components/shared/PageHeader'
import { StatCard } from '@/components/shared/StatCard'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { Card, CardHeader, CardContent } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { dashboardApi } from '@/services/api'
import type { DashboardStats, RecentDocument, RecentChat, SystemHealth } from '@/types'
import { FileText, Network, MessageSquare, Users, Activity, RefreshCw } from 'lucide-react'
import { useNavigate } from 'react-router-dom'

export function DashboardPage() {
  const navigate = useNavigate()
  const [stats, setStats] = useState<DashboardStats | null>(null)
  const [recentDocs, setRecentDocs] = useState<RecentDocument[]>([])
  const [recentChats, setRecentChats] = useState<RecentChat[]>([])
  const [health, setHealth] = useState<SystemHealth[]>([])
  const [loading, setLoading] = useState(false)

  const fetchData = async () => {
    setLoading(true)
    try {
      const [s, d, c, h] = await Promise.all([
        dashboardApi.stats(),
        dashboardApi.recentDocuments(),
        dashboardApi.recentChats(),
        dashboardApi.health(),
      ])
      setStats(s)
      setRecentDocs(d)
      setRecentChats(c)
      setHealth(h)
    } catch (e) {
      // silently fail, keep previous data or empty
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    fetchData()
    const timer = setInterval(fetchData, 30000)
    return () => clearInterval(timer)
  }, [])

  const formatTime = (value: string) => {
    const date = new Date(value)
    const now = new Date()
    const diff = Math.floor((now.getTime() - date.getTime()) / 1000)
    if (diff < 60) return '刚刚'
    if (diff < 3600) return `${Math.floor(diff / 60)}分钟前`
    if (diff < 86400) return `${Math.floor(diff / 3600)}小时前`
    return `${Math.floor(diff / 86400)}天前`
  }

  const healthColor = (status: SystemHealth['status']) => {
    switch (status) {
      case 'active': return 'bg-emerald-500'
      case 'warning': return 'bg-amber-500'
      case 'error': return 'bg-rose-500'
      default: return 'bg-gray-400'
    }
  }

  return (
    <div className="mx-auto w-full max-w-7xl p-4 md:p-8">
      <PageHeader title="工作台" description="系统概览与核心指标">
        <Button variant="ghost" size="sm" onClick={fetchData} isLoading={loading}>
          <RefreshCw className="w-4 h-4" />
        </Button>
      </PageHeader>

      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 mb-6">
        <StatCard
          title="文档总数"
          value={stats?.totalDocuments?.toLocaleString() ?? '-'}
          icon={<FileText className="w-5 h-5" />}
          color="blue"
        />
        <StatCard
          title="图谱实体"
          value={stats?.totalGraphEntities?.toLocaleString() ?? '-'}
          icon={<Network className="w-5 h-5" />}
          color="purple"
        />
        <StatCard
          title="今日问答"
          value={stats?.todayChats?.toLocaleString() ?? '-'}
          icon={<MessageSquare className="w-5 h-5" />}
          color="green"
        />
        <StatCard
          title="活跃用户"
          value={stats?.activeUsers?.toLocaleString() ?? '-'}
          icon={<Users className="w-5 h-5" />}
          color="amber"
        />
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <Card>
          <CardHeader>
            <h3 className="text-base font-semibold text-gray-900">最近文档</h3>
          </CardHeader>
          <CardContent>
            <div className="space-y-3">
              {recentDocs.length === 0 && (
                <p className="text-sm text-gray-400 py-4 text-center">暂无文档</p>
              )}
              {recentDocs.map((doc) => (
                <div
                  key={doc.id}
                  className="flex items-center justify-between py-2 border-b border-gray-50 last:border-0 cursor-pointer hover:bg-gray-50 rounded-lg px-2 -mx-2 transition-colors"
                  onClick={() => navigate(`/documents/${doc.id}`)}
                >
                  <div className="min-w-0">
                    <p className="text-sm font-medium text-gray-900 truncate">{doc.title}</p>
                    <p className="text-xs text-gray-400 mt-0.5">{doc.departmentName} · {formatTime(doc.createdAt)}</p>
                  </div>
                  <StatusBadge status={doc.status} />
                </div>
              ))}
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <h3 className="text-base font-semibold text-gray-900">最近问答</h3>
          </CardHeader>
          <CardContent>
            <div className="space-y-3">
              {recentChats.length === 0 && (
                <p className="text-sm text-gray-400 py-4 text-center">暂无问答记录</p>
              )}
              {recentChats.map((qa) => (
                <div key={qa.id} className="py-2 border-b border-gray-50 last:border-0">
                  <p className="text-sm font-medium text-gray-900">{qa.question}</p>
                  <p className="text-xs text-gray-400 mt-1 truncate">{qa.answer}</p>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
      </div>

      <div className="mt-6">
        <Card>
          <CardHeader>
            <h3 className="text-base font-semibold text-gray-900 flex items-center gap-2">
              <Activity className="w-4 h-4 text-gray-400" />
              系统健康状态
            </h3>
          </CardHeader>
          <CardContent>
            <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
              {health.length === 0 && (
                <p className="text-sm text-gray-400 col-span-full text-center py-4">暂无健康数据</p>
              )}
              {health.map((svc) => (
                <div key={svc.name} className="flex items-center gap-3 p-3 rounded-lg bg-gray-50">
                  <div className={`w-2.5 h-2.5 rounded-full ${healthColor(svc.status)}`} />
                  <span className="text-sm text-gray-700">{svc.name}</span>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
      </div>
    </div>
  )
}
