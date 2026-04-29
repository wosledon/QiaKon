import { PageHeader } from '@/components/shared/PageHeader'
import { Card, CardContent } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { MessageSquare, Clock, Trash2, RefreshCw } from 'lucide-react'
import { useNavigate } from 'react-router-dom'
import { useState, useEffect, useCallback } from 'react'
import { ragApi } from '@/services/api'
import type { ConversationHistoryDto } from '@/types'

export function ChatHistoryPage() {
  const navigate = useNavigate()
  const [history, setHistory] = useState<ConversationHistoryDto[]>([])
  const [loading, setLoading] = useState(false)

  const fetchHistory = useCallback(async () => {
    setLoading(true)
    try {
      const data = await ragApi.getHistory(1, 50)
      setHistory(data.items)
    } catch {
      // ignore
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    fetchHistory()
  }, [fetchHistory])

  const handleDelete = async (e: React.MouseEvent, id: string) => {
    e.stopPropagation()
    e.preventDefault()
    try {
      await ragApi.deleteHistory(id)
      setHistory((prev) => prev.filter((item) => item.id !== id))
    } catch {
      // ignore
    }
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

  return (
    <div className="mx-auto w-full max-w-7xl p-4 md:p-8">
      <PageHeader title="历史会话" description="查看和管理过往问答记录">
        <Button variant="ghost" size="sm" onClick={fetchHistory} isLoading={loading}>
          <RefreshCw className="w-4 h-4" />
        </Button>
      </PageHeader>

      {loading && history.length === 0 ? (
        <div className="text-center py-12 text-gray-400">加载中...</div>
      ) : history.length === 0 ? (
        <div className="text-center py-12 text-gray-400">
          <MessageSquare className="w-10 h-10 mx-auto mb-3 opacity-30" />
          <p>暂无历史会话</p>
        </div>
      ) : (
        <div className="space-y-3">
          {history.map((item) => (
            <Card
              key={item.id}
              className="hover:shadow-md transition-shadow cursor-pointer"
              onClick={() => navigate(`/retrieval/history/${item.id}`)}
            >
              <CardContent className="py-4">
                <div className="flex items-center gap-3 sm:gap-4">
                  <div className="w-10 h-10 rounded-lg bg-blue-50 flex items-center justify-center flex-shrink-0">
                    <MessageSquare className="w-5 h-5 text-blue-600" />
                  </div>
                  <div className="flex-1 min-w-0">
                    <p className="text-sm font-medium text-gray-900 truncate">{item.title}</p>
                    <div className="flex items-center gap-3 mt-1">
                      <span className="text-xs text-gray-400 flex items-center gap-1">
                        <Clock className="w-3 h-3 flex-shrink-0" />
                        {formatTime(item.updatedAt)}
                      </span>
                      <span className="text-xs text-gray-400">{item.messageCount} 轮对话</span>
                    </div>
                  </div>
                  <div className="flex items-center gap-3 flex-shrink-0">
                    <Button
                      variant="ghost"
                      size="sm"
                      className="text-gray-400 hover:text-red-600 px-2"
                      onClick={(e) => handleDelete(e, item.id)}
                      title="删除会话"
                    >
                      <Trash2 className="w-4 h-4" />
                    </Button>
                  </div>
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}
    </div>
  )
}
