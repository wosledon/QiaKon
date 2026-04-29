import { useState, useCallback, useEffect, useRef } from 'react'
import { useNavigate } from 'react-router-dom'
import { Button } from '@/components/ui/Button'
import { Card } from '@/components/ui/Card'
import { Input } from '@/components/ui/Input'
import { Dialog } from '@/components/ui/Dialog'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { PageHeader } from '@/components/shared/PageHeader'
import { documentApi } from '@/services/api'
import type { Document } from '@/types'
import { Upload, Search, Trash2, Eye, Edit, ChevronLeft, ChevronRight, Filter, X } from 'lucide-react'

const statusOptions = [
  { value: '', label: '全部状态' },
  { value: 'pending', label: '待索引' },
  { value: 'indexing', label: '索引中' },
  { value: 'completed', label: '已完成' },
  { value: 'failed', label: '失败' },
]

const departmentOptions = [
  { value: '', label: '全部部门' },
  { value: 'mine', label: '本部门' },
]

const sortOptions = [
  { value: 'createdAt:desc', label: '上传时间 ↓' },
  { value: 'createdAt:asc', label: '上传时间 ↑' },
  { value: 'title:asc', label: '标题 A-Z' },
  { value: 'title:desc', label: '标题 Z-A' },
]

function formatSize(bytes: number): string {
  if (bytes === 0) return '0 B'
  const k = 1024
  const sizes = ['B', 'KB', 'MB', 'GB']
  const i = Math.floor(Math.log(bytes) / Math.log(k))
  return `${parseFloat((bytes / Math.pow(k, i)).toFixed(2))} ${sizes[i]}`
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleString('zh-CN')
}

export function DocumentsListPage() {
  const navigate = useNavigate()
  const [documents, setDocuments] = useState<Document[]>([])
  const [totalCount, setTotalCount] = useState(0)
  const [page, setPage] = useState(1)
  const [pageSize] = useState(20)
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState('')

  // Filters
  const [searchQuery, setSearchQuery] = useState('')
  const [debouncedSearch, setDebouncedSearch] = useState('')
  const [statusFilter, setStatusFilter] = useState('')
  const [departmentFilter, setDepartmentFilter] = useState('')
  const [sortValue, setSortValue] = useState('createdAt:desc')
  const [showFilters, setShowFilters] = useState(false)

  // Batch selection
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set())
  const [confirmDeleteOpen, setConfirmDeleteOpen] = useState(false)
  const [deletingIds, setDeletingIds] = useState<string[]>([])

  const searchTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null)

  const [sortBy, sortOrder] = sortValue.split(':') as [string, 'asc' | 'desc']

  // Debounce search
  useEffect(() => {
    if (searchTimeoutRef.current) {
      clearTimeout(searchTimeoutRef.current)
    }
    searchTimeoutRef.current = setTimeout(() => {
      setDebouncedSearch(searchQuery)
      setPage(1)
    }, 300)
    return () => {
      if (searchTimeoutRef.current) {
        clearTimeout(searchTimeoutRef.current)
      }
    }
  }, [searchQuery])

  const loadDocuments = useCallback(async () => {
    setIsLoading(true)
    setError('')
    try {
      const data = await documentApi.list({
        page,
        pageSize,
        status: statusFilter || undefined,
        department: departmentFilter || undefined,
        search: debouncedSearch || undefined,
        sortBy,
        sortOrder,
      })
      setDocuments(data.items)
      setTotalCount(data.totalCount)
      setSelectedIds(new Set())
    } catch (err) {
      setError(err instanceof Error ? err.message : '加载失败')
    } finally {
      setIsLoading(false)
    }
  }, [page, pageSize, statusFilter, departmentFilter, debouncedSearch, sortBy, sortOrder])

  useEffect(() => {
    loadDocuments()
  }, [loadDocuments])

  const handleDelete = async (id: string) => {
    setDeletingIds([id])
    try {
      await documentApi.delete(id)
      setDocuments((prev) => prev.filter((d) => d.id !== id))
      setTotalCount((c) => c - 1)
      setSelectedIds((prev) => {
        const next = new Set(prev)
        next.delete(id)
        return next
      })
    } catch (err) {
      setError(err instanceof Error ? err.message : '删除失败')
    } finally {
      setDeletingIds([])
      setConfirmDeleteOpen(false)
    }
  }

  const handleBatchDelete = async () => {
    const ids = Array.from(selectedIds)
    setDeletingIds(ids)
    try {
      await documentApi.batchDelete(ids)
      setDocuments((prev) => prev.filter((d) => !selectedIds.has(d.id)))
      setTotalCount((c) => c - ids.length)
      setSelectedIds(new Set())
    } catch (err) {
      setError(err instanceof Error ? err.message : '批量删除失败')
    } finally {
      setDeletingIds([])
      setConfirmDeleteOpen(false)
    }
  }

  const toggleSelect = (id: string) => {
    setSelectedIds((prev) => {
      const next = new Set(prev)
      if (next.has(id)) {
        next.delete(id)
      } else {
        next.add(id)
      }
      return next
    })
  }

  const toggleSelectAll = () => {
    if (selectedIds.size === documents.length && documents.length > 0) {
      setSelectedIds(new Set())
    } else {
      setSelectedIds(new Set(documents.map((d) => d.id)))
    }
  }

  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize))

  const statusLabelMap: Record<string, string> = {
    pending: '待索引',
    indexing: '索引中',
    completed: '已完成',
    failed: '失败',
  }

  const openDeleteConfirm = (ids?: string[]) => {
    if (ids) {
      setDeletingIds(ids)
    }
    setConfirmDeleteOpen(true)
  }

  return (
    <div className="p-4 md:p-8 max-w-7xl mx-auto">
      <PageHeader title="文档管理" description="管理知识库文档，支持上传、检索、索引和权限控制">
        <Button onClick={() => navigate('/documents/upload')}>
          <Upload className="w-4 h-4 mr-2" />
          上传文档
        </Button>
      </PageHeader>

      {/* Search & Filters */}
      <div className="mb-4 space-y-3">
        <div className="flex flex-col sm:flex-row gap-3">
          <div className="relative flex-1">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
            <Input
              placeholder="搜索文档标题..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              className="pl-10"
            />
            {searchQuery && (
              <button
                onClick={() => setSearchQuery('')}
                className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600"
              >
                <X className="w-4 h-4" />
              </button>
            )}
          </div>
          <Button variant="secondary" onClick={() => setShowFilters((v) => !v)}>
            <Filter className="w-4 h-4 mr-2" />
            筛选
          </Button>
          <select
            value={sortValue}
            onChange={(e) => setSortValue(e.target.value)}
            className="px-3 py-2 rounded-lg border border-gray-300 bg-white text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
          >
            {sortOptions.map((opt) => (
              <option key={opt.value} value={opt.value}>
                {opt.label}
              </option>
            ))}
          </select>
        </div>

        {showFilters && (
          <div className="flex flex-wrap gap-3 p-4 bg-gray-50 rounded-lg">
            <select
              value={statusFilter}
              onChange={(e) => { setStatusFilter(e.target.value); setPage(1) }}
              className="px-3 py-2 rounded-lg border border-gray-300 bg-white text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
            >
              {statusOptions.map((opt) => (
                <option key={opt.value} value={opt.value}>
                  {opt.label}
                </option>
              ))}
            </select>
            <select
              value={departmentFilter}
              onChange={(e) => { setDepartmentFilter(e.target.value); setPage(1) }}
              className="px-3 py-2 rounded-lg border border-gray-300 bg-white text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
            >
              {departmentOptions.map((opt) => (
                <option key={opt.value} value={opt.value}>
                  {opt.label}
                </option>
              ))}
            </select>
          </div>
        )}
      </div>

      {/* Batch Actions */}
      {selectedIds.size > 0 && (
        <div className="flex items-center gap-3 mb-4 p-3 bg-blue-50 rounded-lg">
          <span className="text-sm text-blue-700 font-medium">
            已选择 {selectedIds.size} 项
          </span>
          <Button variant="danger" size="sm" onClick={() => openDeleteConfirm()}>
            <Trash2 className="w-4 h-4 mr-1" />
            批量删除
          </Button>
        </div>
      )}

      {error && (
        <div className="mb-4 rounded-lg bg-red-50 px-4 py-3 text-sm text-red-600">{error}</div>
      )}

      {isLoading ? (
        <div className="flex items-center justify-center py-16 text-gray-400">
          <div className="animate-spin h-8 w-8 mr-3 border-2 border-blue-500 border-t-transparent rounded-full" />
          加载中...
        </div>
      ) : documents.length === 0 ? (
        <div className="text-center py-16 text-gray-400">
          <div className="w-16 h-16 mx-auto mb-4 rounded-2xl bg-gray-50 flex items-center justify-center">
            <Search className="w-8 h-8 text-gray-300" />
          </div>
          <p className="text-lg">暂无文档</p>
          <p className="text-sm mt-1">上传文档或调整筛选条件</p>
        </div>
      ) : (
        <>
          {/* Desktop Table */}
          <div className="hidden md:block bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
            <table className="w-full text-sm">
              <thead className="bg-gray-50 border-b border-gray-100">
                <tr>
                  <th className="px-4 py-3 w-10">
                    <input
                      type="checkbox"
                      checked={selectedIds.size === documents.length && documents.length > 0}
                      onChange={toggleSelectAll}
                      className="rounded border-gray-300 text-blue-600 focus:ring-blue-500"
                    />
                  </th>
                  <th className="px-4 py-3 text-left font-medium text-gray-700">文档标题</th>
                  <th className="px-4 py-3 text-left font-medium text-gray-700">所属部门</th>
                  <th className="px-4 py-3 text-left font-medium text-gray-700">状态</th>
                  <th className="px-4 py-3 text-left font-medium text-gray-700">大小</th>
                  <th className="px-4 py-3 text-left font-medium text-gray-700">上传时间</th>
                  <th className="px-4 py-3 text-right font-medium text-gray-700">操作</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {documents.map((doc) => (
                  <tr key={doc.id} className="hover:bg-gray-50 transition-colors">
                    <td className="px-4 py-3">
                      <input
                        type="checkbox"
                        checked={selectedIds.has(doc.id)}
                        onChange={() => toggleSelect(doc.id)}
                        className="rounded border-gray-300 text-blue-600 focus:ring-blue-500"
                      />
                    </td>
                    <td className="px-4 py-3">
                      <div className="font-medium text-gray-900">{doc.title}</div>
                      <div className="text-xs text-gray-500">{doc.type}</div>
                    </td>
                    <td className="px-4 py-3 text-gray-600">{doc.departmentName}</td>
                    <td className="px-4 py-3">
                      <StatusBadge status={doc.indexStatus}>
                        {statusLabelMap[doc.indexStatus] || doc.indexStatus}
                      </StatusBadge>
                    </td>
                    <td className="px-4 py-3 text-gray-600">{formatSize(doc.size)}</td>
                    <td className="px-4 py-3 text-gray-500">{formatDate(doc.createdAt)}</td>
                    <td className="px-4 py-3">
                      <div className="flex items-center justify-end gap-1">
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => navigate(`/documents/${doc.id}`)}
                          title="查看"
                        >
                          <Eye className="w-4 h-4" />
                        </Button>
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => navigate(`/documents/${doc.id}/edit`)}
                          title="编辑"
                        >
                          <Edit className="w-4 h-4" />
                        </Button>
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => openDeleteConfirm([doc.id])}
                          title="删除"
                          className="text-gray-400 hover:text-red-600"
                        >
                          <Trash2 className="w-4 h-4" />
                        </Button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {/* Mobile Cards */}
          <div className="md:hidden space-y-3">
            {documents.map((doc) => (
              <Card key={doc.id} className="p-4">
                <div className="flex items-start gap-3">
                  <input
                    type="checkbox"
                    checked={selectedIds.has(doc.id)}
                    onChange={() => toggleSelect(doc.id)}
                    className="mt-1 rounded border-gray-300 text-blue-600 focus:ring-blue-500"
                  />
                  <div className="flex-1 min-w-0">
                    <p className="font-medium text-gray-900 truncate">{doc.title}</p>
                    <p className="text-sm text-gray-500 mt-0.5">
                      {doc.departmentName} · {formatSize(doc.size)}
                    </p>
                    <div className="flex items-center gap-2 mt-2">
                      <StatusBadge status={doc.indexStatus}>
                        {statusLabelMap[doc.indexStatus] || doc.indexStatus}
                      </StatusBadge>
                      <span className="text-xs text-gray-400">{formatDate(doc.createdAt)}</span>
                    </div>
                    <div className="flex items-center gap-2 mt-3">
                      <Button
                        variant="secondary"
                        size="sm"
                        onClick={() => navigate(`/documents/${doc.id}`)}
                      >
                        <Eye className="w-3 h-3 mr-1" />
                        查看
                      </Button>
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => navigate(`/documents/${doc.id}/edit`)}
                      >
                        <Edit className="w-3 h-3 mr-1" />
                        编辑
                      </Button>
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => openDeleteConfirm([doc.id])}
                        className="text-red-600"
                      >
                        <Trash2 className="w-3 h-3 mr-1" />
                        删除
                      </Button>
                    </div>
                  </div>
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
                <ChevronLeft className="w-4 h-4 mr-1" />
                上一页
              </Button>
              <Button
                variant="secondary"
                size="sm"
                onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                disabled={page >= totalPages || isLoading}
              >
                下一页
                <ChevronRight className="w-4 h-4 ml-1" />
              </Button>
            </div>
          </div>
        </>
      )}

      {/* Confirm Delete Dialog */}
      <Dialog
        open={confirmDeleteOpen}
        onClose={() => setConfirmDeleteOpen(false)}
        title="确认删除"
        footer={
          <>
            <Button variant="secondary" onClick={() => setConfirmDeleteOpen(false)}>
              取消
            </Button>
            <Button
              variant="danger"
              onClick={() => {
                if (deletingIds.length === 1) {
                  handleDelete(deletingIds[0])
                } else {
                  handleBatchDelete()
                }
              }}
              isLoading={deletingIds.length > 0}
            >
              确认删除
            </Button>
          </>
        }
      >
        <p className="text-sm text-gray-600">
          {deletingIds.length > 1
            ? `确定要删除选中的 ${deletingIds.length} 个文档吗？此操作不可恢复。`
            : '确定要删除此文档吗？此操作不可恢复。'}
        </p>
      </Dialog>
    </div>
  )
}
