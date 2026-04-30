import { useEffect, useRef, useState } from 'react'
import { Crosshair, FileText, Highlighter, Loader2, PanelRightClose, X } from 'lucide-react'
import ReactMarkdown from 'react-markdown'
import remarkGfm from 'remark-gfm'
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
  const [highlightFound, setHighlightFound] = useState(false)
  const markdownContainerRef = useRef<HTMLDivElement | null>(null)

  useEffect(() => {
    if (!open || !source?.documentId) {
      return
    }

    let disposed = false
    setLoading(true)
    setError('')
    setHighlightFound(false)

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

  useEffect(() => {
    const container = markdownContainerRef.current
    if (!open || !document?.content || !container) {
      return
    }

    clearSourceHighlights(container)

    const marks = highlightSourceInContainer(container, buildHighlightCandidates(source))
    setHighlightFound(marks.length > 0)

    if (marks.length > 0) {
      requestAnimationFrame(() => {
        scrollToHighlight(marks[0])
      })
    }
  }, [document?.content, open, source])

  const handleJumpToReference = () => {
    const firstHighlight = markdownContainerRef.current?.querySelector<HTMLElement>('mark[data-source-highlight="true"]')
    if (firstHighlight) {
      scrollToHighlight(firstHighlight)
    }
  }

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
                    <div className="mb-1 flex items-center justify-between gap-2">
                      <div className="flex items-center gap-1 font-semibold text-amber-700">
                        <Highlighter className="h-3.5 w-3.5" />
                        引用片段
                      </div>
                      <button
                        type="button"
                        onClick={handleJumpToReference}
                        disabled={!highlightFound}
                        className="inline-flex items-center gap-1 rounded-lg border border-amber-200 bg-white/70 px-2 py-1 text-[11px] font-medium text-amber-700 transition hover:bg-white disabled:cursor-not-allowed disabled:opacity-50"
                      >
                        <Crosshair className="h-3.5 w-3.5" />
                        跳转正文
                      </button>
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

          <div className="min-h-0 flex-1 overflow-y-auto px-5 py-4" data-source-scroll-container="true">
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
                  <div
                    ref={markdownContainerRef}
                    className="markdown-body max-h-none break-words text-sm leading-7 text-gray-800"
                  >
                    <ReactMarkdown
                      remarkPlugins={[remarkGfm]}
                      components={{
                        a: ({ node: _node, ...props }) => (
                          <a {...props} target="_blank" rel="noreferrer" />
                        ),
                      }}
                    >
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

function buildHighlightCandidates(source: Source | null) {
  if (!source) {
    return []
  }

  const uniqueCandidates = new Set<string>()

  for (const raw of [source.text, source.snippet]) {
    for (const candidate of expandHighlightCandidates(raw)) {
      uniqueCandidates.add(candidate)
    }
  }

  return [...uniqueCandidates]
}

function expandHighlightCandidates(value: string | undefined) {
  if (!value) {
    return []
  }

  const plainText = markdownToSearchText(value)
  if (!plainText) {
    return []
  }

  const fragments = plainText
    .split(/(?<=[。！？.!?])\s+|\s{2,}|\n+/)
    .map((item) => item.trim())
    .filter((item) => item.length >= 16)
    .sort((left, right) => right.length - left.length)

  return [plainText, ...fragments.slice(0, 6)]
}

function markdownToSearchText(value: string) {
  return value
    .replace(/```[\s\S]*?```/g, (match) => match.replace(/```/g, ' '))
    .replace(/`([^`]+)`/g, ' $1 ')
    .replace(/!\[([^\]]*)\]\([^)]*\)/g, ' $1 ')
    .replace(/\[([^\]]+)\]\(([^)]+)\)/g, ' $1 ')
    .replace(/^#{1,6}\s+/gm, '')
    .replace(/^>+\s?/gm, '')
    .replace(/^\s*([-*+] |\d+\.\s+)/gm, '')
    .replace(/\|/g, ' ')
    .replace(/[*_~]/g, '')
    .replace(/^-{3,}$/gm, ' ')
    .replace(/<[^>]+>/g, ' ')
    .replace(/\s+/g, ' ')
    .replace(/\.{3}$/g, '')
    .trim()
}

function clearSourceHighlights(container: HTMLElement) {
  const highlights = container.querySelectorAll('mark[data-source-highlight="true"]')
  highlights.forEach((highlight) => {
    const parent = highlight.parentNode
    if (!parent) {
      return
    }

    parent.replaceChild(document.createTextNode(highlight.textContent || ''), highlight)
    parent.normalize()
  })
}

function highlightSourceInContainer(container: HTMLElement, candidates: string[]) {
  for (const candidate of candidates) {
    const marks = highlightSingleCandidate(container, candidate)
    if (marks.length > 0) {
      return marks
    }
  }

  return [] as HTMLElement[]
}

function highlightSingleCandidate(container: HTMLElement, candidate: string) {
  const textNodes = collectTextNodes(container)
  if (textNodes.length === 0) {
    return [] as HTMLElement[]
  }

  const aggregateModel = buildAggregateTextModel(textNodes)
  const aggregateMatch = locateNormalizedMatch(aggregateModel.text, candidate)
  if (!aggregateMatch) {
    return [] as HTMLElement[]
  }

  const marks: HTMLElement[] = []

  for (const segment of aggregateModel.segments) {
    const matchStart = Math.max(segment.start, aggregateMatch.start)
    const matchEnd = Math.min(segment.end, aggregateMatch.end)
    if (matchStart >= matchEnd) {
      continue
    }

    const mark = wrapTextNodeRange(segment.node, matchStart - segment.start, matchEnd - segment.start)
    if (mark) {
      marks.push(mark)
    }
  }

  return marks
}

function collectTextNodes(container: HTMLElement) {
  const textNodes: Text[] = []
  const walker = document.createTreeWalker(container, NodeFilter.SHOW_TEXT, {
    acceptNode(node) {
      const parentElement = node.parentElement
      if (!parentElement) {
        return NodeFilter.FILTER_REJECT
      }

      if (['SCRIPT', 'STYLE', 'NOSCRIPT'].includes(parentElement.tagName)) {
        return NodeFilter.FILTER_REJECT
      }

      if (!node.textContent?.trim()) {
        return NodeFilter.FILTER_SKIP
      }

      return NodeFilter.FILTER_ACCEPT
    },
  })

  let currentNode = walker.nextNode()
  while (currentNode) {
    textNodes.push(currentNode as Text)
    currentNode = walker.nextNode()
  }

  return textNodes
}

function buildAggregateTextModel(textNodes: Text[]) {
  const segments: Array<{ node: Text; start: number; end: number }> = []
  let text = ''
  let cursor = 0

  textNodes.forEach((node, index) => {
    const value = node.textContent || ''
    const start = cursor
    const end = start + value.length

    segments.push({ node, start, end })
    text += value
    cursor = end

    if (index < textNodes.length - 1) {
      text += ' '
      cursor += 1
    }
  })

  return {
    text,
    segments,
  }
}

function locateNormalizedMatch(content: string, target: string) {
  const normalizedContent = normalizeForSearch(content)
  const normalizedTarget = normalizeForSearch(target)
  if (!normalizedContent.text || !normalizedTarget.text) {
    return null
  }

  const startIndex = normalizedContent.text.indexOf(normalizedTarget.text)
  if (startIndex < 0) {
    return null
  }

  const endIndex = startIndex + normalizedTarget.text.length - 1
  const start = normalizedContent.map[startIndex]
  const end = normalizedContent.map[endIndex] + 1
  if (start === undefined || end === undefined) {
    return null
  }

  return { start, end }
}

function normalizeForSearch(value: string) {
  const text = markdownToSearchText(value)
  const chars: string[] = []
  const map: number[] = []
  let previousWasSpace = true

  for (let index = 0; index < text.length; index += 1) {
    const char = text[index]
    if (/\s/.test(char)) {
      if (!previousWasSpace && chars.length > 0) {
        chars.push(' ')
        map.push(index)
      }
      previousWasSpace = true
      continue
    }

    chars.push(char.toLocaleLowerCase())
    map.push(index)
    previousWasSpace = false
  }

  while (chars.length > 0 && chars[chars.length - 1] === ' ') {
    chars.pop()
    map.pop()
  }

  return {
    text: chars.join(''),
    map,
  }
}

function wrapTextNodeRange(node: Text, startOffset: number, endOffset: number) {
  if (startOffset >= endOffset || !node.parentNode) {
    return null
  }

  const matchedNode = startOffset > 0 ? node.splitText(startOffset) : node
  const tailNode = matchedNode.splitText(endOffset - startOffset)
  const mark = document.createElement('mark')
  mark.dataset.sourceHighlight = 'true'
  mark.className = 'rounded bg-amber-200 px-1 py-0.5 text-gray-900 shadow-sm shadow-amber-100'
  matchedNode.parentNode?.insertBefore(mark, matchedNode)
  mark.appendChild(matchedNode)

  if (!tailNode.textContent) {
    tailNode.remove()
  }

  return mark
}

function scrollToHighlight(element: HTMLElement) {
  const scrollContainer = element.closest<HTMLElement>('[data-source-scroll-container="true"]')

  if (scrollContainer) {
    const containerRect = scrollContainer.getBoundingClientRect()
    const elementRect = element.getBoundingClientRect()
    const targetTop =
      scrollContainer.scrollTop +
      (elementRect.top - containerRect.top) -
      scrollContainer.clientHeight / 2 +
      elementRect.height / 2

    scrollContainer.scrollTop = Math.max(0, targetTop)
  } else {
    element.scrollIntoView({
      behavior: 'auto',
      block: 'center',
      inline: 'nearest',
    })
  }

  element.animate(
    [
      { backgroundColor: 'rgba(253, 230, 138, 0.95)' },
      { backgroundColor: 'rgba(252, 211, 77, 0.75)' },
      { backgroundColor: 'rgba(253, 230, 138, 0.95)' },
    ],
    {
      duration: 1400,
      easing: 'ease-out',
    }
  )
}