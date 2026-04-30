import { useMemo, useState } from 'react'
import { ChevronDown, ChevronRight, BrainCircuit, ExternalLink } from 'lucide-react'
import ReactMarkdown from 'react-markdown'
import remarkGfm from 'remark-gfm'
import type { Source } from '@/types'
import { SourceDocumentDrawer } from '@/components/chat/SourceDocumentDrawer'

interface ChatMessageBubbleProps {
  role: 'user' | 'assistant'
  content: string
  sources?: Source[]
  isStreaming?: boolean
  className?: string
}

export function ChatMessageBubble({
  role,
  content,
  sources,
  isStreaming = false,
  className = '',
}: ChatMessageBubbleProps) {
  const isUser = role === 'user'
  const [thoughtExpanded, setThoughtExpanded] = useState(false)
  const [selectedSource, setSelectedSource] = useState<Source | null>(null)
  const parsedContent = useMemo(() => parseAssistantContent(content), [content])
  const hasThought = !isUser && parsedContent.thought.trim().length > 0
  const answerContent = isUser ? content : parsedContent.answer

  return (
    <div className={`flex ${isUser ? 'justify-end' : 'justify-start'} ${className}`}>
      <div
        className={[
          'max-w-[92%] md:max-w-[72%] rounded-2xl px-5 py-3',
          isUser
            ? 'bg-blue-600 text-white rounded-br-md'
            : 'bg-white border border-gray-200 text-gray-800 rounded-bl-md shadow-sm',
        ].join(' ')}
      >
        {isUser ? (
          <p className="whitespace-pre-wrap text-sm leading-7 break-words">{content}</p>
        ) : (
          <div className="space-y-3">
            {hasThought && (
              <div className="rounded-xl border border-amber-200 bg-amber-50/80">
                <button
                  type="button"
                  onClick={() => setThoughtExpanded((prev) => !prev)}
                  className="flex w-full items-center justify-between gap-3 px-3 py-2 text-left"
                >
                  <span className="flex items-center gap-2 text-xs font-semibold text-amber-700">
                    <BrainCircuit className="h-4 w-4" />
                    思考过程
                    {isStreaming && parsedContent.isThinkingActive && (
                      <span className="rounded-full bg-amber-100 px-2 py-0.5 text-[10px] font-medium text-amber-700">
                        思考中
                      </span>
                    )}
                  </span>
                  {thoughtExpanded ? (
                    <ChevronDown className="h-4 w-4 text-amber-600" />
                  ) : (
                    <ChevronRight className="h-4 w-4 text-amber-600" />
                  )}
                </button>

                {thoughtExpanded && (
                  <div className="border-t border-amber-200 px-3 py-3">
                    <div className="markdown-body text-sm text-amber-900">
                      <ReactMarkdown
                        remarkPlugins={[remarkGfm]}
                        components={{
                          a: ({ node: _node, ...props }) => (
                            <a {...props} target="_blank" rel="noreferrer" />
                          ),
                        }}
                      >
                        {parsedContent.thought}
                      </ReactMarkdown>
                    </div>
                  </div>
                )}
              </div>
            )}

            <div className="markdown-body text-sm">
              <ReactMarkdown
                remarkPlugins={[remarkGfm]}
                components={{
                  a: ({ node: _node, ...props }) => (
                    <a {...props} target="_blank" rel="noreferrer" />
                  ),
                }}
              >
                {answerContent || (isStreaming ? ' ' : hasThought ? '' : '暂无回复内容')}
              </ReactMarkdown>
            </div>
            {isStreaming && <span className="inline-block h-4 w-2 animate-pulse rounded-sm bg-blue-500 align-middle" />}
          </div>
        )}

        {!isUser && sources && sources.length > 0 && (
          <div className="mt-4 border-t border-gray-100 pt-3">
            <p className="mb-2 text-xs font-medium text-gray-500">参考来源</p>
            <div className="space-y-2">
              {sources.map((source, index) => (
                <button
                  type="button"
                  key={`${source.documentId}-${index}`}
                  onClick={() => setSelectedSource(source)}
                  className="w-full rounded-lg bg-gray-50 px-3 py-2 text-left transition hover:bg-blue-50"
                >
                  <div className="flex items-center justify-between gap-3">
                    <span className="truncate text-xs font-semibold text-gray-700">{source.title}</span>
                    <span className="inline-flex items-center gap-1 shrink-0 text-[10px] text-gray-400">
                      {(source.score * 100).toFixed(1)}%
                      <ExternalLink className="h-3 w-3" />
                    </span>
                  </div>
                  <p className="mt-1 text-xs leading-5 text-gray-500">{source.snippet}</p>
                </button>
              ))}
            </div>
          </div>
        )}
      </div>

      <SourceDocumentDrawer
        open={selectedSource !== null}
        source={selectedSource}
        onClose={() => setSelectedSource(null)}
      />
    </div>
  )
}

interface ParsedAssistantContent {
  thought: string
  answer: string
  isThinkingActive: boolean
}

function parseAssistantContent(content: string): ParsedAssistantContent {
  if (!content) {
    return { thought: '', answer: '', isThinkingActive: false }
  }

  const thoughtParts: string[] = []
  let answer = ''
  let cursor = 0
  let isThinkingActive = false
  const openTag = /<think>/gi
  const closeTag = /<\/think>/gi

  while (cursor < content.length) {
    openTag.lastIndex = cursor
    const openMatch = openTag.exec(content)
    if (!openMatch) {
      answer += content.slice(cursor)
      break
    }

    const openIndex = openMatch.index
    answer += content.slice(cursor, openIndex)

    closeTag.lastIndex = openTag.lastIndex
    const closeMatch = closeTag.exec(content)
    if (!closeMatch) {
      thoughtParts.push(content.slice(openTag.lastIndex))
      isThinkingActive = true
      cursor = content.length
      break
    }

    thoughtParts.push(content.slice(openTag.lastIndex, closeMatch.index))
    cursor = closeMatch.index + closeMatch[0].length
  }

  return {
    thought: trimIncompleteThinkTagSuffix(thoughtParts.join('\n\n')).trim(),
    answer: trimIncompleteThinkTagSuffix(answer).trim(),
    isThinkingActive,
  }
}

function trimIncompleteThinkTagSuffix(text: string): string {
  if (!text) {
    return ''
  }

  const partialTags = ['<think>', '</think>']
  for (const tag of partialTags) {
    for (let length = tag.length - 1; length > 0; length -= 1) {
      const suffix = tag.slice(0, length)
      if (text.toLowerCase().endsWith(suffix.toLowerCase())) {
        return text.slice(0, -length)
      }
    }
  }

  return text
}
