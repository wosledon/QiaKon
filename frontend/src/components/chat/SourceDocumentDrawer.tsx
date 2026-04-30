import { useEffect, useMemo, useState } from 'react'
import { FileText, Highlighter, Loader2, PanelRightClose, X } from 'lucide-react'
import { documentApi } from '@/services/api'
import type { DocumentDetail, Source } from '@/types'

interface SourceDocumentDrawerProps {
  source: Source | null
  open: boolean
  onClose: () => void
}

export function SourceDocumentDrawer({ source, open, onClose }: SourceDocumentDrawerProps) {
  const [document, setDocument] = useState<DocumentDetail | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')

  useEffect(() => {
    if (!open || !source?.documentId) {
      return
    }

    let disposed = false
    setLoading(true)
    setError('')

    documentApi.get(source.documentId)
      .then((data) => {
        if (!disposed) {
          setDocument(data)
        }
      })
      .catch((err) => {
        if (!disposed) {
          setError(err instanceof Error ? err.message : '加载文档内容失败')
          setDocument(null)
        }
      })
      .finally(() => {
        if (!disposed) {
          setLoading(false)
        }
      })

    return () => {
      disposed = true
    }
  }, [open, source?.documentId])

  const highlightTarget = source?.text?.trim() || source?.snippet?.trim() || ''
  const highlightedContent = useMemo(() => {
    if (!document?.content) {
      return null
    }

    return renderHighlightedContent(document.content, highlightTarget)
  }, [document?.content, highlightTarget])

  return (
    <>
      <div
        className={[
          'fixed inset-0 z-40 bg-slate-950/20 backdrop-blur-[1px] transition-opacity',
          open ? 'pointer-events-auto opacity-100' : 'pointer-events-none opacity-0',
        ].join(' ')}
        onClick={onClose}
      />

      <aside
        className={[
          'fixed right-0 top-0 z-50 h-full w-full max-w-2xl border-l border-gray-200 bg-white shadow-2xl transition-transform duration-300',
          open ? 'translate-x-0' : 'translate-x-full',
        ].join(' ')}
      >
        <div className="flex h-full flex-col">
          <div className="flex items-start justify-between gap-4 border-b border-gray-200 px-5 py-4">
            <div className="min-w-0">
              <div className="flex items-center gap-2 text-xs font-medium text-blue-600">
                <FileText className="h-4 w-4" />
                参考文档预览
              </div>
              <h3 className="mt-2 truncate text-lg font-semibold text-gray-900">
                {document?.title || source?.title || '文档内容'}
              </h3>
              {source && (
                <div className="mt-3 space-y-2">
                  <div className="flex flex-wrap items-center gap-2 text-xs text-gray-500">
                    <span className="rounded-full bg-blue-50 px-2 py-1 text-blue-600">
                      相似度 {(source.score * 100).toFixed(1)}%
                    </span>
                    <span className="rounded-full bg-amber-50 px-2 py-1 text-amber-700">
                      已高亮引用片段
                    </span>
                  </div>
                  <div className="rounded-xl border border-amber-100 bg-amber-50 px-3 py-2 text-xs leading-6 text-amber-900">
                    <div className="mb-1 flex items-center gap-1 font-semibold text-amber-700">
                      <Highlighter className="h-3.5 w-3.5" />
                      引用片段
                    </div>
                    {source.snippet}
                  </div>
                </div>
              )}
            </div>

            <div className="flex items-center gap-2">
              <button
                type="button"
                onClick={onClose}
                className="rounded-lg border border-gray-200 p-2 text-gray-500 transition hover:bg-gray-50 hover:text-gray-700"
                title="收起面板"
              >
                <PanelRightClose className="h-4 w-4" />
              </button>
              <button
                type="button"
                onClick={onClose}
                className="rounded-lg border border-gray-200 p-2 text-gray-500 transition hover:bg-gray-50 hover:text-gray-700"
                title="关闭"
              >
                <X className="h-4 w-4" />
              </button>
            </div>
          </div>

          <div className="min-h-0 flex-1 overflow-y-auto px-5 py-4">
            {loading ? (
              <div className="flex h-full items-center justify-center text-sm text-gray-500">
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                加载文档内容中...
              </div>
            ) : error ? (
              <div className="rounded-xl border border-red-100 bg-red-50 px-4 py-3 text-sm text-red-600">
                {error}
              </div>
            ) : document ? (
              <div className="space-y-4">
                <div className="flex flex-wrap gap-2 text-xs text-gray-500">
                  <span className="rounded-full bg-gray-100 px-2 py-1">{document.type}</span>
                  <span className="rounded-full bg-gray-100 px-2 py-1">{document.departmentName}</span>
                  <span className="rounded-full bg-gray-100 px-2 py-1">{document.accessLevel}</span>
                </div>

                <div className="rounded-2xl border border-gray-200 bg-gray-50 px-4 py-4">
                  <div className="mb-3 text-xs font-medium text-gray-500">文档内容</div>
                  <div className="max-h-none whitespace-pre-wrap break-words text-sm leading-7 text-gray-800">
                    {highlightedContent}
                  </div>
                </div>
              </div>
            ) : (
              <div className="text-sm text-gray-400">暂无文档内容</div>
            )}
          </div>
        </div>
      </aside>
    </>
  )
}

function renderHighlightedContent(content: string, target: string) {
  if (!target) {
    return content
  }

  const normalizedTarget = target.replace(/\s+/g, ' ').trim()
  const exactIndex = content.indexOf(target)
  if (exactIndex >= 0) {
    return (
      <>
        {content.slice(0, exactIndex)}
        <mark className="rounded bg-amber-200 px-1 py-0.5 text-gray-900">{target}</mark>
        {content.slice(exactIndex + target.length)}
      </>
    )
  }

  const normalizedContent = content.replace(/\s+/g, ' ')
  const normalizedIndex = normalizedContent.indexOf(normalizedTarget)
  if (normalizedIndex < 0) {
    return content
  }

  const match = locateNormalizedSegment(content, normalizedTarget, normalizedIndex)
  if (!match) {
    return content
  }

  return (
    <>
      {content.slice(0, match.start)}
      <mark className="rounded bg-amber-200 px-1 py-0.5 text-gray-900">{content.slice(match.start, match.end)}</mark>
      {content.slice(match.end)}
    </>
  )
}

function locateNormalizedSegment(content: string, target: string, targetIndex: number) {
  let normalizedCursor = 0
  let start = -1
  let end = -1

  for (let index = 0; index < content.length; index += 1) {
    if (/\s/.test(content[index])) {
      continue
    }

    if (normalizedCursor === targetIndex) {
      start = index
    }

    normalizedCursor += 1

    if (start >= 0 && normalizedCursor >= targetIndex + target.replace(/\s+/g, '').length) {
      end = index + 1
      break
    }
  }

  if (start < 0 || end < 0) {
    return null
  }

  return { start, end }
}