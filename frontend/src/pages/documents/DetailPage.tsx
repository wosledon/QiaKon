import { useState, useEffect, useCallback } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import ReactMarkdown from 'react-markdown'
import remarkGfm from 'remark-gfm'
import { Button } from '@/components/ui/Button'
import { Card, CardContent } from '@/components/ui/Card'
import { Dialog } from '@/components/ui/Dialog'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { PageHeader } from '@/components/shared/PageHeader'
import { documentApi } from '@/services/api'
import type { DocumentDetail, DocumentChunk } from '@/types'
import {
  ArrowLeft,
  Download,
  RefreshCw,
  Database,
  Edit,
  Trash2,
  FileText,
  AlertCircle,
  CheckCircle,
  Clock,
  Loader2,
} from 'lucide-react'

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

const statusLabelMap: Record<string, string> = {
  pending: '待索引',
  indexing: '索引中',
  completed: '已完成',
  failed: '失败',
}

export function DetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const [doc, setDoc] = useState<DocumentDetail | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState('')
  const [actionLoading, setActionLoading] = useState<string | null>(null)
  const [confirmDelete, setConfirmDelete] = useState(false)
  const [activeTab, setActiveTab] = useState<'preview' | 'chunks'>('preview')

  const loadDoc = useCallback(async () => {
    if (!id) return
    setIsLoading(true)
    setError('')
    try {
      const data = await documentApi.get(id)
      setDoc(data)
    } catch (err) {
      setError(err instanceof Error ? err.message : '加载失败')
    } finally {
      setIsLoading(false)
    }
  }, [id])

  useEffect(() => {
    loadDoc()
  }, [loadDoc])

  const handleReparse = async () => {
    if (!id) return
    setActionLoading('reparse')
    try {
      await documentApi.reparse(id)
      await loadDoc()
    } catch (err) {
      setError(err instanceof Error ? err.message : '重新解析失败')
    } finally {
      setActionLoading(null)
    }
  }

  const handleReindex = async () => {
    if (!id) return
    setActionLoading('reindex')
    try {
      await documentApi.reindex(id)
      await loadDoc()
    } catch (err) {
      setError(err instanceof Error ? err.message : '重新索引失败')
    } finally {
      setActionLoading(null)
    }
  }

  const handleDownload = async () => {
    if (!id) return
    setActionLoading('download')
    try {
      // Note: actual download implementation would use the blob response
      window.open(`${import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000/api'}/documents/${id}/download`, '_blank')
    } catch (err) {
      setError(err instanceof Error ? err.message : '下载失败')
    } finally {
      setActionLoading(null)
    }
  }

  const handleDelete = async () => {
    if (!id) return
    setActionLoading('delete')
    try {
      await documentApi.delete(id)
      navigate('/documents')
    } catch (err) {
      setError(err instanceof Error ? err.message : '删除失败')
      setActionLoading(null)
    }
  }

  if (isLoading) {
    return (
      <div className="p-4 md:p-8 max-w-5xl mx-auto flex items-center justify-center py-24">
        <Loader2 className="w-8 h-8 animate-spin text-blue-500 mr-3" />
        <span className="text-gray-500">加载中...</span>
      </div>
    )
  }

  if (error || !doc) {
    return (
      <div className="p-4 md:p-8 max-w-5xl mx-auto">
        <div className="flex items-center gap-2 rounded-lg bg-red-50 px-4 py-3 text-sm text-red-600">
          <AlertCircle className="w-4 h-4" />
          {error || '文档不存在'}
        </div>
        <Button variant="secondary" className="mt-4" onClick={() => navigate('/documents')}>
          <ArrowLeft className="w-4 h-4 mr-2" />
          返回列表
        </Button>
      </div>
    )
  }

  const getStatusIcon = (status: string) => {
    switch (status) {
      case 'completed':
        return <CheckCircle className="w-5 h-5 text-green-500" />
      case 'indexing':
        return <Loader2 className="w-5 h-5 text-blue-500 animate-spin" />
      case 'pending':
        return <Clock className="w-5 h-5 text-amber-500" />
      case 'failed':
        return <AlertCircle className="w-5 h-5 text-red-500" />
      default:
        return null
    }
  }

  return (
    <div className="p-4 md:p-8 max-w-5xl mx-auto">
      <PageHeader
        title={doc.title}
        description={`${doc.departmentName} · ${formatSize(doc.size)} · 版本 ${doc.version}`}
      >
        <div className="flex items-center gap-2">
          <Button variant="secondary" size="sm" onClick={() => navigate('/documents')}>
            <ArrowLeft className="w-4 h-4 mr-1" />
            返回
          </Button>
          <Button variant="secondary" size="sm" onClick={handleDownload} isLoading={actionLoading === 'download'}>
            <Download className="w-4 h-4 mr-1" />
            下载
          </Button>
          <Button variant="secondary" size="sm" onClick={() => navigate(`/documents/${id}/edit`)}>
            <Edit className="w-4 h-4 mr-1" />
            编辑
          </Button>
          <Button variant="danger" size="sm" onClick={() => setConfirmDelete(true)}>
            <Trash2 className="w-4 h-4 mr-1" />
            删除
          </Button>
        </div>
      </PageHeader>

      {error && (
        <div className="mb-4 flex items-center gap-2 rounded-lg bg-red-50 px-4 py-3 text-sm text-red-600">
          <AlertCircle className="w-4 h-4" />
          {error}
        </div>
      )}

      {/* Document Info Card */}
      <Card className="mb-6">
        <CardContent className="py-5">
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
            <div>
              <p className="text-xs text-gray-500 mb-1">索引状态</p>
              <div className="flex items-center gap-2">
                {getStatusIcon(doc.indexStatus)}
                <StatusBadge status={doc.indexStatus}>
                  {statusLabelMap[doc.indexStatus] || doc.indexStatus}
                </StatusBadge>
              </div>
            </div>
            <div>
              <p className="text-xs text-gray-500 mb-1">文件类型</p>
              <p className="text-sm font-medium text-gray-900">{doc.type}</p>
            </div>
            <div>
              <p className="text-xs text-gray-500 mb-1">创建时间</p>
              <p className="text-sm font-medium text-gray-900">{formatDate(doc.createdAt)}</p>
            </div>
            <div>
              <p className="text-xs text-gray-500 mb-1">更新时间</p>
              <p className="text-sm font-medium text-gray-900">
                {doc.updatedAt ? formatDate(doc.updatedAt) : '-'}
              </p>
            </div>
          </div>

          <div className="flex items-center gap-2 mt-4 pt-4 border-t border-gray-100">
            <Button
              variant="ghost"
              size="sm"
              onClick={handleReparse}
              isLoading={actionLoading === 'reparse'}
            >
              <RefreshCw className="w-4 h-4 mr-1" />
              重新解析
            </Button>
            <Button
              variant="ghost"
              size="sm"
              onClick={handleReindex}
              isLoading={actionLoading === 'reindex'}
            >
              <Database className="w-4 h-4 mr-1" />
              重新索引
            </Button>
          </div>
        </CardContent>
      </Card>

      {/* Tabs */}
      <div className="border-b border-gray-200 mb-4">
        <div className="flex gap-4">
          <button
            onClick={() => setActiveTab('preview')}
            className={`pb-3 text-sm font-medium border-b-2 transition-colors ${
              activeTab === 'preview'
                ? 'border-blue-500 text-blue-600'
                : 'border-transparent text-gray-500 hover:text-gray-700'
            }`}
          >
            <FileText className="w-4 h-4 inline mr-1" />
            内容预览
          </button>
          <button
            onClick={() => setActiveTab('chunks')}
            className={`pb-3 text-sm font-medium border-b-2 transition-colors ${
              activeTab === 'chunks'
                ? 'border-blue-500 text-blue-600'
                : 'border-transparent text-gray-500 hover:text-gray-700'
            }`}
          >
            <Database className="w-4 h-4 inline mr-1" />
            分块列表 ({doc.chunks?.length || 0})
          </button>
        </div>
      </div>

      {activeTab === 'preview' ? (
        <Card>
          <CardContent className="py-5">
            {doc.content ? (
              <div className="markdown-body max-w-none text-gray-700">
                <ReactMarkdown remarkPlugins={[remarkGfm]}>
                  {doc.content}
                </ReactMarkdown>
              </div>
            ) : (
              <div className="text-center py-12 text-gray-400">
                <FileText className="w-10 h-10 mx-auto mb-2" />
                <p>文档内容尚未解析</p>
                <Button variant="secondary" size="sm" className="mt-3" onClick={handleReparse}>
                  立即解析
                </Button>
              </div>
            )}
          </CardContent>
        </Card>
      ) : (
        <div className="space-y-3">
          {doc.chunks && doc.chunks.length > 0 ? (
            doc.chunks.map((chunk: DocumentChunk) => (
              <Card key={chunk.id}>
                <CardContent className="py-4">
                  <div className="flex items-start justify-between gap-4">
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-2 mb-2">
                        <span className="text-xs font-medium bg-gray-100 text-gray-600 px-2 py-0.5 rounded">
                          块 #{chunk.index}
                        </span>
                        <StatusBadge status={chunk.status}>
                          {statusLabelMap[chunk.status] || chunk.status}
                        </StatusBadge>
                        {chunk.chunkingStrategy && (
                          <span className="text-xs rounded bg-blue-50 px-2 py-0.5 text-blue-600">
                            {chunk.chunkingStrategy}
                          </span>
                        )}
                        {chunk.vectorDimension && (
                          <span className="text-xs text-gray-500">
                            {chunk.vectorDimension} 维
                          </span>
                        )}
                      </div>
                      <p className="text-sm text-gray-700 line-clamp-3">
                        {chunk.summary || chunk.content}
                      </p>
                    </div>
                  </div>
                </CardContent>
              </Card>
            ))
          ) : (
            <div className="text-center py-12 text-gray-400">
              <Database className="w-10 h-10 mx-auto mb-2" />
              <p>暂无分块数据</p>
            </div>
          )}
        </div>
      )}

      {/* Confirm Delete Dialog */}
      <Dialog
        open={confirmDelete}
        onClose={() => setConfirmDelete(false)}
        title="确认删除"
        footer={
          <>
            <Button variant="secondary" onClick={() => setConfirmDelete(false)}>
              取消
            </Button>
            <Button
              variant="danger"
              onClick={handleDelete}
              isLoading={actionLoading === 'delete'}
            >
              确认删除
            </Button>
          </>
        }
      >
        <p className="text-sm text-gray-600">
          确定要删除文档「{doc.title}」吗？此操作不可恢复。
        </p>
      </Dialog>
    </div>
  )
}
