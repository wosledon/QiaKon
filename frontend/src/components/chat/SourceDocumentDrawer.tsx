import {
  Children,
  Fragment,
  cloneElement,
  isValidElement,
  type ReactElement,
  type ReactNode,
  useEffect,
  useMemo,
  useState,
} from 'react'
import { FileText, Highlighter, Loader2, PanelRightClose, X } from 'lucide-react'
import ReactMarkdown, { type Components } from 'react-markdown'
import remarkGfm from 'remark-gfm'
import { documentApi } from '@/services/api'
import type { DocumentDetail, Source } from '@/types'

type HighlightableTag =
  | 'p'
  | 'li'
  | 'blockquote'
  | 'h1'
  | 'h2'
  | 'h3'
  | 'h4'
  | 'h5'
  | 'h6'
  | 'td'
  | 'th'
  | 'a'
  | 'strong'
  | 'em'

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
  const markdownComponents = useMemo<Components>(() => {
    const withHighlightedChildren =
      (tagName: HighlightableTag) =>
      ({ children, ...props }: any) => {
        return createElementByTag(
          tagName,
          props,
          renderHighlightedNode(children, highlightTarget)
        )
      }

    return {
      p: withHighlightedChildren('p'),
      li: withHighlightedChildren('li'),
      blockquote: withHighlightedChildren('blockquote'),
      h1: withHighlightedChildren('h1'),
      h2: withHighlightedChildren('h2'),
      h3: withHighlightedChildren('h3'),
      h4: withHighlightedChildren('h4'),
      h5: withHighlightedChildren('h5'),
      h6: withHighlightedChildren('h6'),
      td: withHighlightedChildren('td'),
      th: withHighlightedChildren('th'),
      a: withHighlightedChildren('a'),
      strong: withHighlightedChildren('strong'),
      em: withHighlightedChildren('em'),
      code: ({ inline, children, className, ...props }: any) => {
        if (inline) {
          return (
            <code className={className} {...props}>
              {renderHighlightedNode(children, highlightTarget)}
            </code>
          )
        }

        return (
          <code className={className} {...props}>
            {children}
          </code>
        )
      },
    }
  }, [highlightTarget])

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
                  <div className="markdown-body max-h-none break-words text-sm leading-7 text-gray-800">
                    <ReactMarkdown remarkPlugins={[remarkGfm]} components={markdownComponents}>
                      {document.content}
                    </ReactMarkdown>
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

function createElementByTag(
  tagName: HighlightableTag,
  props: Record<string, unknown>,
  children?: ReactNode
) {
  switch (tagName) {
    case 'p':
      return <p {...props}>{children}</p>
    case 'li':
      return <li {...props}>{children}</li>
    case 'blockquote':
      return <blockquote {...props}>{children}</blockquote>
    case 'h1':
      return <h1 {...props}>{children}</h1>
    case 'h2':
      return <h2 {...props}>{children}</h2>
    case 'h3':
      return <h3 {...props}>{children}</h3>
    case 'h4':
      return <h4 {...props}>{children}</h4>
    case 'h5':
      return <h5 {...props}>{children}</h5>
    case 'h6':
      return <h6 {...props}>{children}</h6>
    case 'td':
      return <td {...props}>{children}</td>
    case 'th':
      return <th {...props}>{children}</th>
    case 'a':
      return <a {...props}>{children}</a>
    case 'strong':
      return <strong {...props}>{children}</strong>
    case 'em':
      return <em {...props}>{children}</em>
    default:
      return <span {...props}>{children}</span>
  }
}

function renderHighlightedNode(node: ReactNode, target: string): ReactNode {
  if (!target || node === null || node === undefined || typeof node === 'boolean') {
    return node
  }

  if (typeof node === 'string') {
    return renderHighlightedText(node, target)
  }

  if (typeof node === 'number') {
    return renderHighlightedText(String(node), target)
  }

  if (Array.isArray(node)) {
    return node.map((child, index) => (
      <Fragment key={index}>{renderHighlightedNode(child, target)}</Fragment>
    ))
  }

  if (isValidElement<{ children?: ReactNode }>(node)) {
    const element = node as ReactElement<{ children?: ReactNode }>
    return cloneElement(element, {
      children: renderHighlightedNode(element.props.children, target),
    })
  }

  return Children.map(node, (child) => renderHighlightedNode(child, target))
}

function renderHighlightedText(content: string, target: string) {
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