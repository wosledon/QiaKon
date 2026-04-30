import { useCallback, useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import {
  ChevronLeft,
  ChevronRight,
  Clock,
  MessageSquare,
  RefreshCw,
  Search,
  Trash2,
} from 'lucide-react'
import { Button } from '@/components/ui/Button'
import { Card, CardContent } from '@/components/ui/Card'
import { ragApi } from '@/services/api'
import type { ConversationHistoryDto } from '@/types'

interface ConversationHistoryPanelProps {
  mode?: 'page' | 'sidebar'
  selectedConversationId?: string
  onSelectConversation?: (conversationId: string) => void
  onDeletedConversation?: (conversationId: string) => void
  collapsed?: boolean
  onToggleCollapse?: () => void
  refreshKey?: string | number
  className?: string
}

export function ConversationHistoryPanel({
  mode = 'page',
  selectedConversationId,
  onSelectConversation,
  onDeletedConversation,
  collapsed = false,
  onToggleCollapse,
  refreshKey,
  className = '',
}: ConversationHistoryPanelProps) {
  const navigate = useNavigate()
  const [history, setHistory] = useState<ConversationHistoryDto[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const [keyword, setKeyword] = useState('')
  const [page, setPage] = useState(1)
  const pageSize = mode === 'sidebar' ? 10 : 12

  const [totalCount, setTotalCount] = useState(0)

  const fetchHistory = useCallback(async () => {
    setLoading(true)
    try {
      setError('')
      const data = await ragApi.getHistory(page, pageSize, keyword || undefined)
      setHistory(data.items)
      setTotalCount(data.totalCount)
    } catch (err) {
      setError(err instanceof Error ? err.message : '加载历史会话失败')
    } finally {
      setLoading(false)
    }
  }, [keyword, page, pageSize])

  useEffect(() => {
    fetchHistory()
  }, [fetchHistory, refreshKey])

  const handleDelete = async (event: React.MouseEvent, id: string) => {
    event.stopPropagation()
    event.preventDefault()

    try {
      await ragApi.deleteHistory(id)
      setHistory((prev) => prev.filter((item) => item.id !== id))
      setTotalCount((prev) => Math.max(0, prev - 1))
      onDeletedConversation?.(id)
    } catch {
      // ignore
    }
  }

  const handleSelect = (id: string) => {
    if (onSelectConversation) {
      onSelectConversation(id)
      return
    }

    navigate(`/retrieval/chat?conversationId=${id}`)
  }

  const formatTime = (dateStr: string) => {
    const date = new Date(dateStr)
    const now = new Date()
    const diff = Math.floor((now.getTime() - date.getTime()) / 1000)
    if (diff < 60) return '刚刚'
    if (diff < 3600) return `${Math.floor(diff / 60)}分钟前`
    if (diff < 86400) return `${Math.floor(diff / 3600)}小时前`
    return `${Math.floor(diff / 86400)}天前`
  }

  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize))

  if (mode === 'sidebar' && collapsed) {
    return (
      <div className={`flex h-full min-h-0 flex-col ${className}`}>
        <div className="border-b border-gray-200 px-2 py-3">
          <Button
            variant="ghost"
            size="sm"
            className="w-full px-0"
            onClick={onToggleCollapse}
            title="展开历史栏"
          >
            <ChevronRight className="h-4 w-4" />
          </Button>
        </div>

        <div className="min-h-0 flex-1 overflow-y-auto px-2 py-3">
          {loading && history.length === 0 ? (
            <div className="py-4 text-center text-xs text-gray-400">加载中</div>
          ) : history.length === 0 ? (
            <div className="py-4 text-center text-xs text-gray-400">暂无</div>
          ) : (
            <div className="space-y-2">
              {history.map((item) => {
                const isSelected = selectedConversationId === item.id

                return (
                  <button
                    key={item.id}
                    type="button"
                    onClick={() => handleSelect(item.id)}
                    title={item.title}
                    className={[
                      'flex h-10 w-10 items-center justify-center rounded-xl border transition-all',
                      isSelected
                        ? 'border-blue-200 bg-blue-50 text-blue-600'
                        : 'border-gray-200 bg-white text-gray-500 hover:border-gray-300 hover:bg-gray-50',
                    ].join(' ')}
                  >
                    <MessageSquare className="h-4 w-4" />
                  </button>
                )
              })}
            </div>
          )}
        </div>

        <div className="border-t border-gray-200 px-2 py-3">
          <Button
            variant="ghost"
            size="sm"
            className="w-full px-0"
            onClick={fetchHistory}
            disabled={loading}
            title="刷新历史记录"
          >
            <RefreshCw className={loading ? 'h-4 w-4 animate-spin' : 'h-4 w-4'} />
          </Button>
        </div>
      </div>
    )
  }

  return (
    <div className={`flex h-full min-h-0 flex-col ${className}`}>
      <div className={mode === 'sidebar' ? 'border-b border-gray-200 px-4 py-4' : ''}>
        {mode === 'sidebar' && (
          <div className="mb-3 flex items-center justify-between gap-3">
            <div>
              <h2 className="text-sm font-semibold text-gray-900">历史会话</h2>
              <p className="mt-1 text-xs text-gray-500">左侧快速切换，右侧继续聊天</p>
            </div>
            <div className="flex items-center gap-1">
              <Button variant="ghost" size="sm" onClick={fetchHistory} disabled={loading} title="刷新历史记录">
                <RefreshCw className={loading ? 'h-4 w-4 animate-spin' : 'h-4 w-4'} />
              </Button>
              <Button variant="ghost" size="sm" onClick={onToggleCollapse} title="收起历史栏">
                <ChevronLeft className="h-4 w-4" />
              </Button>
            </div>
          </div>
        )}

        <div className={mode === 'sidebar' ? 'space-y-3' : 'flex flex-col gap-3 sm:flex-row sm:items-center'}>
          <div className={mode === 'sidebar' ? 'w-full' : 'w-full sm:max-w-md'}>
            <div className="relative">
              <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-gray-400" />
              <input
                type="text"
                placeholder="搜索会话标题"
                value={keyword}
                onChange={(event) => {
                  setKeyword(event.target.value)
                  setPage(1)
                }}
                className="h-11 w-full rounded-xl border border-gray-300 bg-white pl-10 pr-4 text-sm text-gray-900 placeholder-gray-400 shadow-sm transition-all duration-200 focus:border-transparent focus:outline-none focus:ring-2 focus:ring-blue-500"
              />
            </div>
          </div>

          {mode === 'page' ? (
            <div className="flex items-center gap-2">
              <Button variant="secondary" onClick={fetchHistory} isLoading={loading}>
                <Search className="mr-2 h-4 w-4" />
                搜索
              </Button>
              <Button variant="ghost" size="sm" onClick={fetchHistory} disabled={loading} title="刷新历史记录">
                <RefreshCw className={loading ? 'h-4 w-4 animate-spin' : 'h-4 w-4'} />
              </Button>
            </div>
          ) : null}
        </div>
      </div>

      {error && (
        <div className={mode === 'sidebar' ? 'px-4 pt-4' : 'mt-4'}>
          <div className="rounded-lg bg-red-50 px-4 py-3 text-sm text-red-600">{error}</div>
        </div>
      )}

      <div className={`min-h-0 flex-1 ${mode === 'sidebar' ? 'overflow-y-auto px-3 py-3' : 'mt-4'}`}>
        {loading && history.length === 0 ? (
          <div className="py-12 text-center text-gray-400">加载中...</div>
        ) : history.length === 0 ? (
          <div className="py-12 text-center text-gray-400">
            <MessageSquare className="mx-auto mb-3 h-10 w-10 opacity-30" />
            <p>暂无历史会话</p>
          </div>
        ) : mode === 'sidebar' ? (
          <div className="space-y-2">
            {history.map((item) => {
              const isSelected = selectedConversationId === item.id

              return (
                <div
                  key={item.id}
                  className={[
                    'group flex items-start gap-3 rounded-2xl border px-3 py-3 transition-all',
                    isSelected
                      ? 'border-blue-200 bg-blue-50 shadow-sm'
                      : 'border-gray-200 bg-white hover:border-gray-300 hover:bg-gray-50',
                  ].join(' ')}
                >
                  <button
                    type="button"
                    onClick={() => handleSelect(item.id)}
                    className="flex min-w-0 flex-1 items-start gap-3 text-left"
                  >
                    <div className={[
                      'mt-0.5 flex h-9 w-9 flex-shrink-0 items-center justify-center rounded-xl',
                      isSelected ? 'bg-blue-100 text-blue-600' : 'bg-gray-100 text-gray-500',
                    ].join(' ')}>
                      <MessageSquare className="h-4 w-4" />
                    </div>

                    <div className="min-w-0 flex-1">
                      <p className={[
                        'truncate text-sm font-medium',
                        isSelected ? 'text-blue-900' : 'text-gray-900',
                      ].join(' ')}>
                        {item.title}
                      </p>
                      <div className="mt-1 flex items-center gap-2 text-xs text-gray-400">
                        <span className="inline-flex items-center gap-1">
                          <Clock className="h-3 w-3 flex-shrink-0" />
                          {formatTime(item.updatedAt)}
                        </span>
                        <span>{item.messageCount} 轮</span>
                      </div>
                    </div>
                  </button>

                  <button
                    type="button"
                    className="rounded-lg p-1.5 text-gray-400 opacity-0 transition hover:bg-red-50 hover:text-red-600 group-hover:opacity-100"
                    title="删除会话"
                    onClick={(event) => handleDelete(event, item.id)}
                  >
                    <Trash2 className="h-4 w-4" />
                  </button>
                </div>
              )
            })}
          </div>
        ) : (
          <div className="space-y-3">
            {history.map((item) => (
              <Card
                key={item.id}
                className="cursor-pointer transition-shadow hover:shadow-md"
                onClick={() => navigate(`/retrieval/history/${item.id}`)}
              >
                <CardContent className="py-4">
                  <div className="flex items-center gap-3 sm:gap-4">
                    <div className="flex h-10 w-10 flex-shrink-0 items-center justify-center rounded-lg bg-blue-50">
                      <MessageSquare className="h-5 w-5 text-blue-600" />
                    </div>
                    <div className="min-w-0 flex-1">
                      <p className="truncate text-sm font-medium text-gray-900">{item.title}</p>
                      <div className="mt-1 flex items-center gap-3">
                        <span className="flex items-center gap-1 text-xs text-gray-400">
                          <Clock className="h-3 w-3 flex-shrink-0" />
                          {formatTime(item.updatedAt)}
                        </span>
                        <span className="text-xs text-gray-400">{item.messageCount} 轮对话</span>
                      </div>
                    </div>
                    <div className="flex flex-shrink-0 items-center gap-3">
                      <Button
                        variant="secondary"
                        size="sm"
                        onClick={(event) => {
                          event.stopPropagation()
                          navigate(`/retrieval/chat?conversationId=${item.id}`)
                        }}
                      >
                        继续
                      </Button>
                      <Button
                        variant="ghost"
                        size="sm"
                        className="px-2 text-gray-400 hover:text-red-600"
                        onClick={(event) => handleDelete(event, item.id)}
                        title="删除会话"
                      >
                        <Trash2 className="h-4 w-4" />
                      </Button>
                    </div>
                  </div>
                </CardContent>
              </Card>
            ))}
          </div>
        )}
      </div>

      <div className={mode === 'sidebar' ? 'border-t border-gray-200 px-3 py-3' : 'mt-6 flex items-center justify-between'}>
        <div className={mode === 'sidebar' ? 'mb-2 text-xs text-gray-500' : 'text-sm text-gray-500'}>
          共 {totalCount} 条，第 {page} / {totalPages} 页
        </div>
        <div className="flex items-center gap-2">
          <Button
            variant="secondary"
            size="sm"
            onClick={() => setPage((prev) => Math.max(1, prev - 1))}
            disabled={page <= 1 || loading}
          >
            <ChevronLeft className="mr-1 h-4 w-4" />
            上一页
          </Button>
          <Button
            variant="secondary"
            size="sm"
            onClick={() => setPage((prev) => Math.min(totalPages, prev + 1))}
            disabled={page >= totalPages || loading}
          >
            下一页
            <ChevronRight className="ml-1 h-4 w-4" />
          </Button>
        </div>
      </div>
    </div>
  )
}