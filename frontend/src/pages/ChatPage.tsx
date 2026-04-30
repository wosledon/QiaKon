import { useEffect, useRef, useState, type FormEvent } from 'react'
import { useLocation, useSearchParams } from 'react-router-dom'
import { BrainCircuit, MessageSquareMore } from 'lucide-react'
import { ChatMessageBubble } from '@/components/chat/ChatMessageBubble'
import { ConversationHistoryPanel } from '@/components/chat/ConversationHistoryPanel'
import { Button } from '@/components/ui/Button'
import { Input } from '@/components/ui/Input'
import { chatApi, ragApi } from '@/services/api'
import type { ChatResponseData, ConversationDetailDto, Source } from '@/types'

interface Turn {
  id: string
  role: 'user' | 'assistant'
  content: string
  sources?: Source[]
  turns?: number
  isStreaming?: boolean
}

export function ChatPage() {
  const location = useLocation()
  const [searchParams, setSearchParams] = useSearchParams()
  const [turns, setTurns] = useState<Turn[]>([])
  const [input, setInput] = useState('')
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState('')
  const [enableThinking, setEnableThinking] = useState(false)
  const [conversationId, setConversationId] = useState<string | undefined>(undefined)
  const [historyRefreshToken, setHistoryRefreshToken] = useState(0)
  const [isHistoryCollapsed, setIsHistoryCollapsed] = useState(false)
  const scrollRef = useRef<HTMLDivElement>(null)
  const abortRef = useRef<AbortController | null>(null)
  const loadingConversationIdRef = useRef<string | null>(null)

  const conversationIdFromUrl = searchParams.get('conversationId') ?? undefined

  useEffect(() => {
    scrollRef.current?.scrollTo({ top: scrollRef.current.scrollHeight, behavior: 'smooth' })
  }, [turns])

  useEffect(() => {
    return () => abortRef.current?.abort()
  }, [])

  useEffect(() => {
    if (!conversationIdFromUrl) {
      setConversationId(undefined)
      setTurns([])
      return
    }

    if (loadingConversationIdRef.current === conversationIdFromUrl) {
      return
    }

    loadingConversationIdRef.current = conversationIdFromUrl
    setIsLoading(true)
    setError('')

    ragApi.getDetail(conversationIdFromUrl)
      .then((detail) => {
        setConversationId(detail.id)
        setTurns(mapConversationTurns(detail))
      })
      .catch((err) => {
        setError(err instanceof Error ? err.message : '加载历史会话失败')
      })
      .finally(() => {
        setIsLoading(false)
        loadingConversationIdRef.current = null
      })
  }, [conversationIdFromUrl])

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault()
    if (!input.trim() || isLoading) return

    abortRef.current?.abort()
    const query = input.trim()
    const userTurn: Turn = {
      id: crypto.randomUUID(),
      role: 'user',
      content: query,
    }
    const assistantTurnId = crypto.randomUUID()
    const assistantTurn: Turn = {
      id: assistantTurnId,
      role: 'assistant',
      content: '',
      isStreaming: true,
    }

    setTurns((prev) => [...prev, userTurn])
    setTurns((prev) => [...prev, assistantTurn])
    setInput('')
    setIsLoading(true)
    setError('')

    const abortController = new AbortController()
    abortRef.current = abortController

    try {
      let streamedResponse: ChatResponseData | null = null

      await chatApi.sendStream({
        query,
        conversationId,
        enableThinking,
      }, {
        signal: abortController.signal,
        onChunk: ({ delta }) => {
          setTurns((prev) => prev.map((turn) => (
            turn.id === assistantTurnId
              ? { ...turn, content: `${turn.content}${delta}`, isStreaming: true }
              : turn
          )))
        },
        onDone: (payload) => {
          streamedResponse = payload
          setConversationId(payload.conversationId)
          setHistoryRefreshToken((prev) => prev + 1)
          setSearchParams((prev) => {
            const next = new URLSearchParams(prev)
            next.set('conversationId', payload.conversationId)
            return next
          })
          setTurns((prev) => prev.map((turn) => (
            turn.id === assistantTurnId
              ? {
                  ...turn,
                  content: payload.response,
                  sources: payload.sources,
                  turns: payload.turns,
                  isStreaming: false,
                }
              : turn
          )))
        },
      })

      if (!streamedResponse) {
        throw new Error('流式响应未完成，请稍后重试')
      }
    } catch (err) {
      if (err instanceof DOMException && err.name === 'AbortError') {
        return
      }

      const message = err instanceof Error ? err.message : '请求失败'
      setError(message)
      setTurns((prev) => prev.filter((turn) => turn.id !== assistantTurnId))
    } finally {
      setIsLoading(false)
      abortRef.current = null
    }
  }

  const handleNewChat = () => {
    abortRef.current?.abort()
    setTurns([])
    setConversationId(undefined)
    setError('')
    setInput('')
    setSearchParams((prev) => {
      const next = new URLSearchParams(prev)
      next.delete('conversationId')
      return next
    })
  }

  const handleSelectConversation = (selectedId: string) => {
    abortRef.current?.abort()
    setError('')
    setInput('')
    setSearchParams((prev) => {
      const next = new URLSearchParams(prev)
      next.set('conversationId', selectedId)
      return next
    })
  }

  const handleDeletedConversation = (deletedId: string) => {
    if ((conversationId ?? conversationIdFromUrl) !== deletedId) {
      return
    }

    handleNewChat()
  }

  return (
    <div className="flex h-[calc(100vh-4rem)] bg-gray-50">
      <aside className={[
        'hidden flex-shrink-0 border-r border-gray-200 bg-white transition-[width] duration-300 lg:block',
        isHistoryCollapsed ? 'w-[64px]' : 'w-[320px]',
      ].join(' ')}>
        <ConversationHistoryPanel
          mode="sidebar"
          selectedConversationId={conversationIdFromUrl}
          onSelectConversation={handleSelectConversation}
          onDeletedConversation={handleDeletedConversation}
          collapsed={isHistoryCollapsed}
          onToggleCollapse={() => setIsHistoryCollapsed((prev) => !prev)}
          refreshKey={historyRefreshToken}
        />
      </aside>

      <div className="flex min-w-0 flex-1 flex-col">
        {/* Header */}
        <div className="flex items-center justify-between border-b border-gray-200 bg-white px-4 py-3">
          <div className="space-y-1 text-sm text-gray-500">
            {conversationId ? (
              <span>会话 ID: <span className="font-mono text-gray-700">{conversationId.slice(0, 8)}...</span></span>
            ) : (
              <span>{location.pathname === '/' ? '首页对话' : '新会话'}</span>
            )}
            <p className="text-xs text-gray-400">回复支持 Markdown，已切换为流式输出。</p>
            <p className="text-xs text-gray-400">支持 <code className="rounded bg-gray-100 px-1 py-0.5">&lt;think&gt;</code> 思考标签，默认折叠显示。</p>
          </div>
          <div className="flex items-center gap-2">
            <Button variant="secondary" size="sm" onClick={handleNewChat}>
              <MessageSquareMore className="mr-1 h-4 w-4" />
              新对话
            </Button>
          </div>
        </div>

        {/* Messages */}
        <div ref={scrollRef} className="flex-1 overflow-y-auto p-4 space-y-4">
          {turns.length === 0 && (
            <div className="flex h-full flex-col items-center justify-center text-gray-400">
              <svg className="mb-4 h-16 w-16" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M8 10h.01M12 10h.01M16 10h.01M9 16H5a2 2 0 01-2-2V6a2 2 0 012-2h14a2 2 0 012 2v8a2 2 0 01-2 2h-5l-5 5v-5z" />
              </svg>
              <p className="text-lg">开始提问吧</p>
              <p className="mt-1 text-sm">基于知识图谱的智能问答</p>
              <p className="mt-2 text-xs text-gray-400 lg:hidden">桌面端可在左侧直接查看历史会话列表。</p>
            </div>
          )}

          {turns.map((msg) => (
            <ChatMessageBubble
              key={msg.id}
              role={msg.role}
              content={msg.content}
              sources={msg.sources}
              isStreaming={msg.isStreaming}
            />
          ))}

          {isLoading && turns.length === 0 && (
            <div className="flex justify-start">
              <div className="rounded-2xl rounded-bl-md border border-gray-200 bg-white px-5 py-3 shadow-sm">
                <div className="flex items-center space-x-2">
                  <div className="h-2 w-2 animate-bounce rounded-full bg-gray-400" />
                  <div className="h-2 w-2 animate-bounce rounded-full bg-gray-400 [animation-delay:0.2s]" />
                  <div className="h-2 w-2 animate-bounce rounded-full bg-gray-400 [animation-delay:0.4s]" />
                </div>
              </div>
            </div>
          )}

          {error && (
            <div className="rounded-lg bg-red-50 px-4 py-3 text-center text-sm text-red-600">
              {error}
            </div>
          )}
        </div>

        {/* Input */}
        <div className="border-t border-gray-200 bg-white p-4">
          <form onSubmit={handleSubmit} className="mx-auto max-w-4xl space-y-3">
            <div className="flex items-end gap-3">
              <div className="flex-1">
                <Input
                  placeholder="输入问题..."
                  value={input}
                  onChange={(e) => setInput(e.target.value)}
                  disabled={isLoading}
                  className="w-full"
                />
              </div>
              <Button type="submit" disabled={isLoading || !input.trim()} size="md">
                <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 19l9 2-9-18-9 18 9-2zm0 0v-8" />
                </svg>
              </Button>
            </div>

            <button
              type="button"
              onClick={() => setEnableThinking((prev) => !prev)}
              disabled={isLoading}
              className={[
                'inline-flex items-center gap-3 rounded-xl border px-3 py-2 text-left text-sm transition-all',
                enableThinking
                  ? 'border-blue-200 bg-blue-50 text-blue-700 shadow-sm'
                  : 'border-gray-200 bg-gray-50 text-gray-600 hover:border-gray-300 hover:bg-gray-100',
                isLoading ? 'cursor-not-allowed opacity-60' : 'cursor-pointer',
              ].join(' ')}
            >
              <span className={[
                'flex h-5 w-5 flex-shrink-0 items-center justify-center rounded-full border',
                enableThinking ? 'border-blue-500 bg-blue-500 text-white' : 'border-gray-300 bg-white text-transparent',
              ].join(' ')}>
                <span className="text-[11px]">✓</span>
              </span>
              <span>
                <span className={[
                  'flex items-center gap-2 font-medium',
                  enableThinking ? 'text-blue-800' : 'text-gray-800',
                ].join(' ')}>
                  <BrainCircuit className={enableThinking ? 'h-4 w-4 text-blue-600' : 'h-4 w-4 text-gray-500'} />
                  深度思考
                  {enableThinking && (
                    <span className="rounded-full bg-blue-100 px-2 py-0.5 text-[10px] font-semibold text-blue-700">
                      已开启
                    </span>
                  )}
                </span>
              </span>
            </button>
          </form>
        </div>
      </div>
    </div>
  )
}

function mapConversationTurns(detail: ConversationDetailDto): Turn[] {
  return detail.messages.map((message) => ({
    id: message.id,
    role: message.role === 'assistant' ? 'assistant' : 'user',
    content: message.content,
    sources: message.sources,
  }))
}
