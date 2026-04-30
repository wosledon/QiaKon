import { useState, useCallback, useEffect } from 'react'
import { PageHeader } from '@/components/shared/PageHeader'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { Select } from '@/components/ui/Select'
import { Input } from '@/components/ui/Input'
import { graphApi } from '@/services/api'
import type { GraphRelation } from '@/types'
import { Search } from 'lucide-react'

const relationTypeOptions = [
  { value: '', label: '全部类型' },
  { value: '包含', label: '包含' },
  { value: '子章节', label: '子章节' },
  { value: '下一段', label: '下一段' },
]

function formatDate(iso?: string): string {
  if (!iso) return '-'
  return new Date(iso).toLocaleString('zh-CN')
}

export function GraphRelationsPage() {
  const [relations, setRelations] = useState<GraphRelation[]>([])
  const [totalCount, setTotalCount] = useState(0)
  const [page, setPage] = useState(1)
  const pageSize = 20
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState('')

  const [typeFilter, setTypeFilter] = useState('')
  const [sourceFilter, setSourceFilter] = useState('')
  const [targetFilter, setTargetFilter] = useState('')

  const loadRelations = useCallback(async () => {
    setIsLoading(true)
    setError('')
    try {
      const data = await graphApi.relations({
        page,
        pageSize,
        type: typeFilter || undefined,
        sourceId: sourceFilter || undefined,
        targetId: targetFilter || undefined,
      })
      setRelations(data.items)
      setTotalCount(data.totalCount)
    } catch (err) {
      setError(err instanceof Error ? err.message : '加载失败')
    } finally {
      setIsLoading(false)
    }
  }, [page, pageSize, typeFilter, sourceFilter, targetFilter])

  useEffect(() => {
    loadRelations()
  }, [loadRelations])

  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize))

  const handleSearch = () => {
    setPage(1)
    loadRelations()
  }

  return (
    <div className="p-4 md:p-8 max-w-6xl mx-auto">
      <PageHeader title="关系浏览" description="关系由文档结构自动生成，用于追踪文档与章节之间的连接" />

      <div className="mb-4 rounded-xl border border-blue-100 bg-blue-50 px-4 py-3 text-sm text-blue-700">
        当前图谱关系主要来自文档包含、章节层级和片段顺序，不建议在此手工维护。
      </div>

      {/* Filters */}
      <div className="flex flex-col sm:flex-row gap-3 mb-4">
        <Select
          value={typeFilter}
          onChange={(e) => { setTypeFilter(e.target.value); setPage(1) }}
          options={relationTypeOptions}
          className="w-40"
        />
        <Input
          placeholder="源实体ID或名称"
          value={sourceFilter}
          onChange={(e) => { setSourceFilter(e.target.value); setPage(1) }}
          className="w-48"
        />
        <Input
          placeholder="目标实体ID或名称"
          value={targetFilter}
          onChange={(e) => { setTargetFilter(e.target.value); setPage(1) }}
          className="w-48"
        />
        <Button variant="secondary" size="sm" onClick={handleSearch}>
          <Search className="w-4 h-4" />
        </Button>
      </div>

      {error && (
        <div className="mb-4 rounded-lg bg-red-50 px-4 py-3 text-sm text-red-600">{error}</div>
      )}

      {isLoading ? (
        <div className="flex items-center justify-center py-16 text-gray-400">
          <svg className="animate-spin h-8 w-8 mr-3" fill="none" viewBox="0 0 24 24">
            <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
            <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
          </svg>
          加载中...
        </div>
      ) : relations.length === 0 ? (
        <div className="text-center py-16 text-gray-400">
          <p className="text-lg">暂没关系</p>
          <p className="text-sm mt-1">文档自动建图后，这里会展示结构化关系</p>
        </div>
      ) : (
        <>
          <div className="grid gap-3">
            {relations.map((relation) => (
              <Card key={relation.id} className="flex flex-col sm:flex-row sm:items-center sm:justify-between p-4">
                <div className="min-w-0">
                  <p className="font-medium text-gray-900">
                    <span>{relation.sourceName}</span>
                    <span className="mx-2 text-gray-400">→</span>
                    <span>{relation.targetName}</span>
                  </p>
                  <p className="text-sm text-gray-500 mt-0.5">
                    {relation.type} · {formatDate(relation.createdAt)}
                  </p>
                </div>
              </Card>
            ))}
          </div>

          {/* Pagination */}
          <div className="flex items-center justify-between mt-6">
            <span className="text-sm text-gray-500">
              共 {totalCount} 条，第 {page} / {totalPages} 页
            </span>
            <div className="flex items-center gap-2">
              <Button
                variant="secondary"
                size="sm"
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                disabled={page <= 1 || isLoading}
              >
                上一页
              </Button>
              <Button
                variant="secondary"
                size="sm"
                onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                disabled={page >= totalPages || isLoading}
              >
                下一页
              </Button>
            </div>
          </div>
        </>
      )}
    </div>
  )
}
