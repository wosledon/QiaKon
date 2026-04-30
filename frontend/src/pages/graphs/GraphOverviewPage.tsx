import { useState, useEffect, useCallback } from 'react'
import { PageHeader } from '@/components/shared/PageHeader'
import { StatCard } from '@/components/shared/StatCard'
import { Card, CardHeader, CardContent } from '@/components/ui/Card'
import {
  Network,
  Link2,
  Globe,
  Building2,
  RefreshCw,
  MousePointerClick,
  Eye,
  EyeOff,
} from 'lucide-react'
import { graphApi } from '@/services/api'
import type {
  GraphOverview,
  GraphTypeDistribution,
  GraphPreviewData,
  GraphPreviewNode,
} from '@/types'
import { GraphPreview } from '@/components/graphs/GraphPreview'

export function GraphOverviewPage() {
  const [overview, setOverview] = useState<GraphOverview | null>(null)
  const [previewData, setPreviewData] = useState<GraphPreviewData | null>(null)
  const [entityTypes, setEntityTypes] = useState<GraphTypeDistribution[]>([])
  const [relationTypes, setRelationTypes] = useState<GraphTypeDistribution[]>(
    []
  )
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState('')

  const [selectedNode, setSelectedNode] = useState<GraphPreviewNode | null>(
    null
  )

  const load = useCallback(async () => {
    setIsLoading(true)
    setError('')
    setSelectedNode(null)
    try {
      const [ov, pv, et, rt] = await Promise.all([
        graphApi.overview(),
        graphApi.preview(),
        graphApi.entityTypes(),
        graphApi.relationTypes(),
      ])
      setOverview(ov)
      setPreviewData(pv)

      const convertDistribution = (data: unknown): GraphTypeDistribution[] => {
        const d = data as { distribution?: Record<string, number> } | undefined
        if (!d?.distribution) return []
        const entries = Object.entries(d.distribution)
        const total = entries.reduce((sum, [, count]) => sum + count, 0)
        return entries.map(([type, count]) => ({
          type,
          count,
          percentage: total > 0 ? Math.round((count / total) * 100) : 0,
        }))
      }

      setEntityTypes(convertDistribution(et))
      setRelationTypes(convertDistribution(rt))
    } catch (err) {
      setError(err instanceof Error ? err.message : '加载失败')
    } finally {
      setIsLoading(false)
    }
  }, [])

  useEffect(() => {
    load()
  }, [load])

  const formatNum = (n?: number) => (n ?? 0).toLocaleString('zh-CN')

  return (
    <div className="p-4 md:p-8 max-w-7xl mx-auto">
      <PageHeader title="图谱概览" description="知识图谱由文档索引自动构建，展示整体统计与结构分布" />

      <div className="mb-4 rounded-xl border border-blue-100 bg-blue-50 px-4 py-3 text-sm text-blue-700">
        当前图谱以自动生成的文档、章节、片段结构为主；如需更新图谱，请在文档页重新解析或重建索引。
      </div>

      {error && (
        <div className="mb-4 rounded-lg bg-red-50 px-4 py-3 text-sm text-red-600 flex items-center justify-between">
          <span>{error}</span>
          <button
            onClick={load}
            className="flex items-center gap-1 text-red-600 hover:text-red-700 font-medium transition-colors"
          >
            <RefreshCw className="w-4 h-4" />
            重试
          </button>
        </div>
      )}

      {/* Stats */}
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

      {/* Graph Preview */}
      <Card className="mb-6">
        <CardHeader>
          <div className="flex items-center justify-between">
            <h3 className="text-base font-semibold text-gray-900">图谱预览</h3>
            {previewData && (
              <span className="text-xs text-gray-500">
                展示 {previewData.nodes.length}/{previewData.totalNodeCount ?? previewData.nodes.length} 个节点，
                {previewData.edges.length}/{previewData.totalEdgeCount ?? previewData.edges.length} 条边
              </span>
            )}
          </div>
        </CardHeader>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="h-[400px] flex items-center justify-center">
              <div className="flex flex-col items-center gap-3">
                <div className="w-8 h-8 border-2 border-purple-200 border-t-purple-500 rounded-full animate-spin" />
                <p className="text-sm text-gray-400">加载图谱中...</p>
              </div>
            </div>
          ) : previewData && previewData.nodes.length > 0 ? (
            <div className="flex flex-col xl:flex-row">
              <div className="flex-1 min-w-0 p-4">
                <GraphPreview
                  data={previewData}
                  selectedNodeId={selectedNode?.id ?? null}
                  onNodeSelect={setSelectedNode}
                />
                <p className="text-center text-xs text-gray-400 mt-2 flex items-center justify-center gap-1">
                  <MousePointerClick className="w-3 h-3" />
                  点击节点查看详情，悬停高亮关联
                </p>
              </div>
              {selectedNode && (
                <div className="w-full xl:w-64 border-t xl:border-t-0 xl:border-l border-gray-200 p-4 bg-gray-50/60">
                  <h4 className="text-sm font-semibold text-gray-900 mb-3">节点详情</h4>
                  <div className="space-y-3">
                    <div>
                      <p className="text-xs text-gray-500">名称</p>
                      <p className="text-sm font-medium text-gray-900 mt-0.5">
                        {selectedNode.name}
                      </p>
                    </div>
                    <div>
                      <p className="text-xs text-gray-500">类型</p>
                      <div className="flex items-center gap-1.5 mt-0.5">
                        <span className={`h-2.5 w-2.5 rounded-full ${getTypeBadgeClass(selectedNode.type)}`} />
                        <p className="text-sm text-gray-700">{selectedNode.type}</p>
                      </div>
                    </div>
                    {selectedNode.departmentName && (
                      <div>
                        <p className="text-xs text-gray-500">所属部门</p>
                        <p className="text-sm text-gray-700 mt-0.5">
                          {selectedNode.departmentName}
                        </p>
                      </div>
                    )}
                    <div className="flex gap-6">
                      <div>
                        <p className="text-xs text-gray-500">访问权限</p>
                        <div className="flex items-center gap-1 mt-0.5">
                          {selectedNode.isPublic ? (
                            <>
                              <Eye className="w-3.5 h-3.5 text-emerald-500" />
                              <span className="text-sm text-gray-700">公开</span>
                            </>
                          ) : (
                            <>
                              <EyeOff className="w-3.5 h-3.5 text-gray-400" />
                              <span className="text-sm text-gray-700">内部</span>
                            </>
                          )}
                        </div>
                      </div>
                      <div>
                        <p className="text-xs text-gray-500">连接数</p>
                        <p className="text-sm text-gray-700 mt-0.5">
                          {selectedNode.degree ?? 0}
                        </p>
                      </div>
                    </div>
                  </div>
                  <button
                    onClick={() => setSelectedNode(null)}
                    className="mt-4 w-full text-xs text-gray-500 hover:text-gray-700 py-1.5 border border-gray-200 rounded-md hover:bg-white transition-colors cursor-pointer"
                  >
                    关闭详情
                  </button>
                </div>
              )}
            </div>
          ) : (
            <div className="h-[400px] flex flex-col items-center justify-center text-gray-400">
              <Network className="w-12 h-12 mb-3 opacity-30" />
              <p className="text-sm">暂无图谱数据</p>
              <p className="text-xs text-gray-300 mt-1">
                知识图谱中还没有足够的节点用于预览
              </p>
            </div>
          )}
        </CardContent>
      </Card>

      {/* Type Distribution */}
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
              {isLoading && entityTypes.length === 0 && (
                <div className="space-y-3 animate-pulse">
                  {[...Array(4)].map((_, i) => (
                    <div key={i} className="flex items-center gap-3">
                      <div className="w-16 h-4 bg-gray-100 rounded" />
                      <div className="flex-1 bg-gray-100 rounded-full h-2" />
                      <div className="w-16 h-4 bg-gray-100 rounded" />
                    </div>
                  ))}
                </div>
              )}
              {entityTypes.map((t) => (
                <div key={t.type} className="flex items-center gap-3">
                  <span className="text-sm text-gray-600 w-20 truncate">{t.type}</span>
                  <progress
                    className="flex-1 overflow-hidden rounded-full [&::-moz-progress-bar]:bg-purple-500 [&::-webkit-progress-bar]:bg-gray-100 [&::-webkit-progress-value]:bg-purple-500"
                    max={100}
                    value={Math.min(t.percentage, 100)}
                  />
                  <span className="text-sm font-medium text-gray-900 w-16 text-right tabular-nums">
                    {t.count}
                  </span>
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
              {isLoading && relationTypes.length === 0 && (
                <div className="space-y-3 animate-pulse">
                  {[...Array(4)].map((_, i) => (
                    <div key={i} className="flex items-center gap-3">
                      <div className="w-16 h-4 bg-gray-100 rounded" />
                      <div className="flex-1 bg-gray-100 rounded-full h-2" />
                      <div className="w-16 h-4 bg-gray-100 rounded" />
                    </div>
                  ))}
                </div>
              )}
              {relationTypes.map((t) => (
                <div key={t.type} className="flex items-center gap-3">
                  <span className="text-sm text-gray-600 w-20 truncate">{t.type}</span>
                  <progress
                    className="flex-1 overflow-hidden rounded-full [&::-moz-progress-bar]:bg-blue-500 [&::-webkit-progress-bar]:bg-gray-100 [&::-webkit-progress-value]:bg-blue-500"
                    max={100}
                    value={Math.min(t.percentage, 100)}
                  />
                  <span className="text-sm font-medium text-gray-900 w-16 text-right tabular-nums">
                    {t.count}
                  </span>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
      </div>
    </div>
  )
}

function getTypeBadgeClass(type: string): string {
  switch (type) {
    case '文档':
      return 'bg-blue-500'
    case '章节':
      return 'bg-purple-500'
    case '片段':
      return 'bg-emerald-500'
    default:
      return 'bg-gray-400'
  }
}
