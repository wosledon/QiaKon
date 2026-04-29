import { useState, useEffect } from 'react'
import { PageHeader } from '@/components/shared/PageHeader'
import { StatCard } from '@/components/shared/StatCard'
import { Card, CardHeader, CardContent } from '@/components/ui/Card'
import { Network, Link2, Globe, Building2 } from 'lucide-react'
import { graphApi } from '@/services/api'
import type { GraphOverview, GraphTypeDistribution } from '@/types'

export function GraphOverviewPage() {
  const [overview, setOverview] = useState<GraphOverview | null>(null)
  const [entityTypes, setEntityTypes] = useState<GraphTypeDistribution[]>([])
  const [relationTypes, setRelationTypes] = useState<GraphTypeDistribution[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState('')

  useEffect(() => {
    const load = async () => {
      setIsLoading(true)
      setError('')
      try {
        const [ov, et, rt] = await Promise.all([
          graphApi.overview(),
          graphApi.entityTypes(),
          graphApi.relationTypes(),
        ])
        setOverview(ov)
        // 将API返回的{ distribution: {Type: count} }转换为数组格式
        const convertDistribution = (data: any) => {
          if (!data?.distribution) return []
          const entries = Object.entries(data.distribution as Record<string, number>)
          const total = entries.reduce((sum, [, count]) => sum + count, 0)
          return entries.map(([type, count]) => ({
            type,
            count,
            percentage: total > 0 ? Math.round((count / total) * 100) : 0
          }))
        }
        setEntityTypes(convertDistribution(et))
        setRelationTypes(convertDistribution(rt))
      } catch (err) {
        setError(err instanceof Error ? err.message : '加载失败')
      } finally {
        setIsLoading(false)
      }
    }
    load()
  }, [])

  const formatNum = (n?: number) => (n ?? 0).toLocaleString('zh-CN')

  return (
    <div className="p-4 md:p-8 max-w-6xl mx-auto">
      <PageHeader title="图谱概览" description="知识图谱整体统计与分布" />

      {error && (
        <div className="mb-4 rounded-lg bg-red-50 px-4 py-3 text-sm text-red-600">{error}</div>
      )}

      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 mb-6">
        <StatCard
          title="实体总数"
          value={isLoading ? '-' : formatNum(overview?.totalEntities)}
          icon={<Network className="w-5 h-5" />}
          color="purple"
        />
        <StatCard
          title="关系总数"
          value={isLoading ? '-' : formatNum(overview?.totalRelations)}
          icon={<Link2 className="w-5 h-5" />}
          color="blue"
        />
        <StatCard
          title="公开实体"
          value={isLoading ? '-' : formatNum(overview?.publicEntities)}
          icon={<Globe className="w-5 h-5" />}
          color="green"
        />
        <StatCard
          title="本部门实体"
          value={isLoading ? '-' : formatNum(overview?.departmentEntities)}
          icon={<Building2 className="w-5 h-5" />}
          color="amber"
        />
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <Card>
          <CardHeader>
            <h3 className="text-base font-semibold text-gray-900">实体类型分布</h3>
          </CardHeader>
          <CardContent>
            <div className="space-y-3">
              {entityTypes.length === 0 && !isLoading && (
                <p className="text-sm text-gray-400">暂无数据</p>
              )}
              {entityTypes.map((t) => (
                <div key={t.type} className="flex items-center gap-3">
                  <span className="text-sm text-gray-600 w-16">{t.type}</span>
                  <div className="flex-1 bg-gray-100 rounded-full h-2">
                    <div
                      className="bg-purple-500 h-2 rounded-full"
                      style={{ width: `${Math.min(t.percentage, 100)}%` }}
                    />
                  </div>
                  <span className="text-sm font-medium text-gray-900 w-16 text-right">{t.count}</span>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <h3 className="text-base font-semibold text-gray-900">关系类型分布</h3>
          </CardHeader>
          <CardContent>
            <div className="space-y-3">
              {relationTypes.length === 0 && !isLoading && (
                <p className="text-sm text-gray-400">暂无数据</p>
              )}
              {relationTypes.map((t) => (
                <div key={t.type} className="flex items-center gap-3">
                  <span className="text-sm text-gray-600 w-16">{t.type}</span>
                  <div className="flex-1 bg-gray-100 rounded-full h-2">
                    <div
                      className="bg-blue-500 h-2 rounded-full"
                      style={{ width: `${Math.min(t.percentage, 100)}%` }}
                    />
                  </div>
                  <span className="text-sm font-medium text-gray-900 w-16 text-right">{t.count}</span>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
      </div>
    </div>
  )
}
