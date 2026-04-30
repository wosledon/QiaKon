import { useState, useCallback, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { PageHeader } from '@/components/shared/PageHeader'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { Input } from '@/components/ui/Input'
import { Select } from '@/components/ui/Select'
import { graphApi } from '@/services/api'
import type { GraphEntity } from '@/types'
import { Search, Eye } from 'lucide-react'

const entityTypeOptions = [
  { value: '', label: '全部类型' },
  { value: '文档', label: '文档' },
  { value: '章节', label: '章节' },
  { value: '片段', label: '片段' },
]

function formatDate(iso?: string): string {
  if (!iso) return '-'
  return new Date(iso).toLocaleString('zh-CN')
}

export function GraphEntitiesPage() {
  const navigate = useNavigate()
  const [entities, setEntities] = useState<GraphEntity[]>([])
  const [totalCount, setTotalCount] = useState(0)
  const [page, setPage] = useState(1)
  const pageSize = 20
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState('')

  const [search, setSearch] = useState('')
  const [typeFilter, setTypeFilter] = useState('')
  const [deptFilter, setDeptFilter] = useState('')

  const loadEntities = useCallback(async () => {
    setIsLoading(true)
    setError('')
    try {
      const data = await graphApi.entities({
        page,
        pageSize,
        search: search || undefined,
        type: typeFilter || undefined,
        department: deptFilter || undefined,
      })
      setEntities(data.items)
      setTotalCount(data.totalCount)
    } catch (err) {
      setError(err instanceof Error ? err.message : '加载失败')
    } finally {
      setIsLoading(false)
    }
  }, [page, pageSize, search, typeFilter, deptFilter])

  useEffect(() => {
    loadEntities()
  }, [loadEntities])

  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize))

  const handleSearch = () => {
    setPage(1)
    loadEntities()
  }

  return (
    <div className="p-4 md:p-8 max-w-6xl mx-auto">
      <PageHeader title="实体浏览" description="实体由文档自动生成，当前页面以浏览和检索为主" />

      <div className="mb-4 rounded-xl border border-blue-100 bg-blue-50 px-4 py-3 text-sm text-blue-700">
        如需更新实体，请修改来源文档后重新解析或重建索引；这里不再作为手工维护入口。
      </div>

      {/* Filters */}
      <div className="flex flex-col sm:flex-row gap-3 mb-4">
        <div className="flex-1 flex gap-2">
          <Input
            placeholder="搜索实体名称"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && handleSearch()}
            className="flex-1"
          />
          <Button variant="secondary" size="sm" onClick={handleSearch}>
            <Search className="w-4 h-4" />
          </Button>
        </div>
        <div className="flex gap-2">
          <Select
            value={typeFilter}
            onChange={(e) => { setTypeFilter(e.target.value); setPage(1) }}
            options={entityTypeOptions}
            className="w-36"
          />
          <Input
            placeholder="部门筛选"
            value={deptFilter}
            onChange={(e) => { setDeptFilter(e.target.value); setPage(1) }}
            className="w-36"
          />
        </div>
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
      ) : entities.length === 0 ? (
        <div className="text-center py-16 text-gray-400">
          <p className="text-lg">暂无实体</p>
          <p className="text-sm mt-1">上传并索引文档后会自动生成图谱实体</p>
        </div>
      ) : (
        <>
          <div className="grid gap-3">
            {entities.map((entity) => (
              <Card key={entity.id} className="flex flex-col sm:flex-row sm:items-center sm:justify-between p-4">
                <div className="min-w-0">
                  <p className="font-medium text-gray-900">{entity.name}</p>
                  <p className="text-sm text-gray-500 mt-0.5">
                    {entity.type} · {entity.departmentName || '-'} · {formatDate(entity.createdAt)}
                  </p>
                </div>
                <div className="flex items-center gap-2 mt-3 sm:mt-0 flex-shrink-0">
                  <Button variant="ghost" size="sm" onClick={() => navigate(`/graphs/entities/${entity.id}`)}>
                    <Eye className="w-4 h-4" />
                  </Button>
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
