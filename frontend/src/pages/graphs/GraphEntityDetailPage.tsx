import { useState, useEffect } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { PageHeader } from '@/components/shared/PageHeader'
import { Button } from '@/components/ui/Button'
import { Card, CardHeader, CardContent } from '@/components/ui/Card'
import { graphApi } from '@/services/api'
import type { GraphEntity, GraphRelation } from '@/types'
import { ArrowLeft, FileText } from 'lucide-react'

interface RelationGroup {
  type: string
  count: number
  relations: GraphRelation[]
}

function formatDate(iso?: string): string {
  if (!iso) return '-'
  return new Date(iso).toLocaleString('zh-CN')
}

function formatJson(obj: Record<string, unknown>): string {
  try {
    return JSON.stringify(obj, null, 2)
  } catch {
    return String(obj)
  }
}

export function GraphEntityDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()

  const [entity, setEntity] = useState<GraphEntity | null>(null)
  const [relationGroups, setRelationGroups] = useState<RelationGroup[]>([])
  const [neighbors, setNeighbors] = useState<GraphEntity[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState('')

  useEffect(() => {
    if (!id) return
    const load = async () => {
      setIsLoading(true)
      setError('')
      try {
        const [entityData, outgoingRelations, incomingRelations, neighborData] = await Promise.all([
          graphApi.entity(id),
          graphApi.relations({ sourceId: id, pageSize: 1000 }),
          graphApi.relations({ targetId: id, pageSize: 1000 }),
          graphApi.queryNeighbors({ entityId: id, direction: 'both' }),
        ])
        setEntity(entityData)

        const relationData = [
          ...outgoingRelations.items,
          ...incomingRelations.items.filter((relation) => !outgoingRelations.items.some((existing) => existing.id === relation.id)),
        ]

        // Group relations by type
        const groups: Record<string, RelationGroup> = {}
        relationData.forEach((r) => {
          if (!groups[r.type]) {
            groups[r.type] = { type: r.type, count: 0, relations: [] }
          }
          groups[r.type].count++
          groups[r.type].relations.push(r)
        })
        setRelationGroups(Object.values(groups))

        setNeighbors(neighborData.neighbors.map((n) => n.entity))
      } catch (err) {
        setError(err instanceof Error ? err.message : '加载失败')
      } finally {
        setIsLoading(false)
      }
    }
    load()
  }, [id])

  const sourceDocumentId = typeof entity?.properties?.sourceDocumentId === 'string'
    ? entity.properties.sourceDocumentId
    : undefined
  const sourceDocumentTitle = typeof entity?.properties?.sourceDocumentTitle === 'string'
    ? entity.properties.sourceDocumentTitle
    : undefined

  if (isLoading) {
    return (
      <div className="p-4 md:p-8 max-w-6xl mx-auto">
        <div className="flex items-center justify-center py-16 text-gray-400">
          <svg className="animate-spin h-8 w-8 mr-3" fill="none" viewBox="0 0 24 24">
            <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
            <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
          </svg>
          加载中...
        </div>
      </div>
    )
  }

  if (!entity) {
    return (
      <div className="p-4 md:p-8 max-w-6xl mx-auto">
        <div className="text-center py-16 text-gray-400">
          <p className="text-lg">实体不存在或已删除</p>
          <Button variant="secondary" className="mt-4" onClick={() => navigate('/graphs/entities')}>
            返回列表
          </Button>
        </div>
      </div>
    )
  }

  return (
    <div className="p-4 md:p-8 max-w-6xl mx-auto">
      <div className="flex items-center gap-2 mb-4">
        <Button variant="ghost" size="sm" onClick={() => navigate('/graphs/entities')}>
          <ArrowLeft className="w-4 h-4 mr-1" />
          返回
        </Button>
      </div>

      <PageHeader title={entity.name} description={`类型: ${entity.type}`} />

      {error && (
        <div className="mb-4 rounded-lg bg-red-50 px-4 py-3 text-sm text-red-600">{error}</div>
      )}

      <div className="mb-4 rounded-xl border border-blue-100 bg-blue-50 px-4 py-3 text-sm text-blue-700">
        当前实体以文档自动生成结果为主，页面提供来源追溯与关系浏览，不再作为手工编辑入口。
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <div className="lg:col-span-2 space-y-6">
          <Card>
            <CardHeader>
              <h3 className="text-base font-semibold text-gray-900">实体信息</h3>
            </CardHeader>
            <CardContent className="space-y-3">
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div>
                  <p className="text-sm text-gray-500">名称</p>
                  <p className="text-sm font-medium text-gray-900">{entity.name}</p>
                </div>
                <div>
                  <p className="text-sm text-gray-500">类型</p>
                  <p className="text-sm font-medium text-gray-900">{entity.type}</p>
                </div>
                <div>
                  <p className="text-sm text-gray-500">所属部门</p>
                  <p className="text-sm font-medium text-gray-900">{entity.departmentName || '-'}</p>
                </div>
                <div>
                  <p className="text-sm text-gray-500">创建时间</p>
                  <p className="text-sm font-medium text-gray-900">{formatDate(entity.createdAt)}</p>
                </div>
              </div>
              <div>
                <p className="text-sm text-gray-500 mb-1">属性</p>
                <pre className="text-sm bg-gray-50 rounded-lg p-3 overflow-x-auto text-gray-800">
                  {formatJson(entity.properties)}
                </pre>
              </div>
              {sourceDocumentId && (
                <div className="rounded-lg border border-gray-200 bg-gray-50 px-4 py-3">
                  <p className="text-sm font-medium text-gray-900">来源文档</p>
                  <p className="mt-1 text-sm text-gray-500">{sourceDocumentTitle || sourceDocumentId}</p>
                  <Button variant="secondary" size="sm" className="mt-3" onClick={() => navigate(`/documents/${sourceDocumentId}`)}>
                    <FileText className="mr-1 h-4 w-4" />
                    查看来源文档
                  </Button>
                </div>
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <h3 className="text-base font-semibold text-gray-900">关系统计</h3>
            </CardHeader>
            <CardContent>
              {relationGroups.length === 0 ? (
                <p className="text-sm text-gray-400">暂无关联关系</p>
              ) : (
                <div className="space-y-4">
                  {relationGroups.map((group) => (
                    <div key={group.type}>
                      <div className="flex items-center justify-between mb-2">
                        <span className="text-sm font-medium text-gray-700">{group.type}</span>
                        <span className="text-xs text-gray-500">{group.count} 条</span>
                      </div>
                      <div className="space-y-1">
                        {group.relations.slice(0, 5).map((r) => (
                          <div key={r.id} className="text-sm text-gray-600 flex items-center gap-1">
                            <span className="font-medium">{r.sourceName}</span>
                            <span className="text-gray-400">→</span>
                            <span className="font-medium">{r.targetName}</span>
                          </div>
                        ))}
                        {group.relations.length > 5 && (
                          <p className="text-xs text-gray-400">还有 {group.relations.length - 5} 条...</p>
                        )}
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </CardContent>
          </Card>
        </div>

        <div className="space-y-6">
          <Card>
            <CardHeader>
              <h3 className="text-base font-semibold text-gray-900">邻居实体</h3>
            </CardHeader>
            <CardContent>
              {neighbors.length === 0 ? (
                <p className="text-sm text-gray-400">暂无邻居实体</p>
              ) : (
                <div className="space-y-2">
                  {neighbors.map((neighbor) => (
                    <button
                      key={neighbor.id}
                      className="w-full text-left px-3 py-2 rounded-lg hover:bg-gray-50 transition-colors"
                      onClick={() => navigate(`/graphs/entities/${neighbor.id}`)}
                    >
                      <p className="text-sm font-medium text-gray-900">{neighbor.name}</p>
                      <p className="text-xs text-gray-500">{neighbor.type}</p>
                    </button>
                  ))}
                </div>
              )}
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  )
}
