import { useCallback, useEffect, useMemo, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { ArrowLeft, Download, PencilLine, Play, Trash2 } from 'lucide-react'
import { ChatMessageBubble } from '@/components/chat/ChatMessageBubble'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { PageHeader } from '@/components/shared/PageHeader'
import { Button } from '@/components/ui/Button'
import { Card, CardContent } from '@/components/ui/Card'
import { Input } from '@/components/ui/Input'
import { ragApi } from '@/services/api'
import type { ConversationDetailDto } from '@/types'

export function ChatHistoryDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const [detail, setDetail] = useState<ConversationDetailDto | null>(null)
  const [title, setTitle] = useState('')
  const [isEditingTitle, setIsEditingTitle] = useState(false)
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')
  const [deleteOpen, setDeleteOpen] = useState(false)

  const loadDetail = useCallback(async () => {
    if (!id) return
    setLoading(true)
    setError('')
    try {
      const data = await ragApi.getDetail(id)
      setDetail(data)
      setTitle(data.title)
    } catch (err) {
      setError(err instanceof Error ? err.message : '加载会话详情失败')
    } finally {
      setLoading(false)
    }
  }, [id])

  useEffect(() => {
    loadDetail()
  }, [loadDetail])

  const formatTime = useMemo(() => (value: string) => new Date(value).toLocaleString('zh-CN'), [])

  const handleSaveTitle = async () => {
    if (!id || !title.trim()) return
    setSaving(true)
    try {
      const data = await ragApi.updateTitle(id, title.trim())
      setDetail(data)
      setTitle(data.title)
      setIsEditingTitle(false)
    } catch (err) {
      setError(err instanceof Error ? err.message : '更新标题失败')
    } finally {
      setSaving(false)
    }
  }

  const handleExport = async () => {
    if (!id) return
    try {
      const blob = await ragApi.exportMarkdown(id)
      const url = URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = url
      link.download = `${(detail?.title || 'conversation').replace(/[\\/:*?"<>|]/g, '_')}.md`
      link.click()
      URL.revokeObjectURL(url)
    } catch (err) {
      setError(err instanceof Error ? err.message : '导出失败')
    }
  }

  const handleDelete = async () => {
    if (!id) return
    setSaving(true)
    try {
      await ragApi.deleteHistory(id)
      navigate('/retrieval/history')
    } catch (err) {
      setError(err instanceof Error ? err.message : '删除失败')
    } finally {
      setSaving(false)
      setDeleteOpen(false)
    }
  }

  return (
    <div className="mx-auto w-full max-w-6xl p-4 md:p-8">
      <div className="mb-4">
        <Button variant="ghost" size="sm" onClick={() => navigate('/retrieval/history')}>
          <ArrowLeft className="mr-1 h-4 w-4" />
          返回历史记录
        </Button>
      </div>

      <PageHeader
        title={detail?.title || '会话详情'}
        description={detail ? `创建于 ${formatTime(detail.createdAt)} · 最近更新 ${formatTime(detail.updatedAt)}` : '查看完整对话与引用来源'}
      >
        <div className="flex flex-wrap items-center gap-2">
          <Button variant="secondary" size="sm" onClick={() => setIsEditingTitle((prev) => !prev)}>
            <PencilLine className="mr-1 h-4 w-4" />
            改标题
          </Button>
          {id && (
            <Button variant="secondary" size="sm" onClick={() => navigate(`/retrieval/chat?conversationId=${id}`)}>
              <Play className="mr-1 h-4 w-4" />
              继续对话
            </Button>
          )}
          <Button variant="secondary" size="sm" onClick={handleExport}>
            <Download className="mr-1 h-4 w-4" />
            导出 Markdown
          </Button>
          <Button variant="danger" size="sm" onClick={() => setDeleteOpen(true)}>
            <Trash2 className="mr-1 h-4 w-4" />
            删除
          </Button>
        </div>
      </PageHeader>

      {isEditingTitle && (
        <Card className="mb-4">
          <CardContent className="flex flex-col gap-3 py-4 sm:flex-row sm:items-center">
            <Input value={title} onChange={(e) => setTitle(e.target.value)} placeholder="输入新的会话标题" />
            <div className="flex items-center gap-2">
              <Button onClick={handleSaveTitle} isLoading={saving} disabled={!title.trim()}>
                保存
              </Button>
              <Button variant="secondary" onClick={() => { setIsEditingTitle(false); setTitle(detail?.title || '') }}>
                取消
              </Button>
            </div>
          </CardContent>
        </Card>
      )}

      {error && (
        <div className="mb-4 rounded-lg bg-red-50 px-4 py-3 text-sm text-red-600">{error}</div>
      )}

      {loading ? (
        <div className="py-16 text-center text-gray-400">加载中...</div>
      ) : !detail ? (
        <div className="py-16 text-center text-gray-400">未找到对应的历史会话</div>
      ) : (
        <div className="space-y-4">
          {detail.messages.map((message) => (
            <div key={message.id}>
              <div className={`mb-1 text-xs text-gray-400 ${message.role === 'assistant' ? 'text-left' : 'text-right'}`}>
                {formatTime(message.createdAt)}
              </div>
              <ChatMessageBubble
                role={message.role === 'assistant' ? 'assistant' : 'user'}
                content={message.content}
                sources={message.sources}
              />
            </div>
          ))}
        </div>
      )}

      <ConfirmDialog
        open={deleteOpen}
        title="删除会话"
        message="删除后将无法恢复，确认继续吗？"
        onConfirm={handleDelete}
        onCancel={() => setDeleteOpen(false)}
        isLoading={saving}
      />
    </div>
  )
}
