import { useMemo, useState, useCallback } from 'react'
import type { GraphPreviewData, GraphPreviewNode } from '@/types'

interface GraphPreviewProps {
  data: GraphPreviewData
  onNodeSelect?: (node: GraphPreviewNode | null) => void
  selectedNodeId?: string | null
}

const TYPE_COLORS: Record<string, string> = {
  Person: '#6366f1',
  Organization: '#0ea5e9',
  Location: '#10b981',
  Event: '#f59e0b',
  Technology: '#ec4899',
  Concept: '#8b5cf6',
  Department: '#14b8a6',
  Product: '#f97316',
  Project: '#84cc16',
  Document: '#64748b',
  Role: '#a855f7',
}

export function getTypeColor(type: string): string {
  return TYPE_COLORS[type] ?? '#6b7280'
}

interface LayoutResult {
  positions: Map<string, { x: number; y: number }>
  centerId: string
}

function computeLayout(data: GraphPreviewData, width: number, height: number): LayoutResult {
  const { nodes, edges } = data
  if (nodes.length === 0) return { positions: new Map(), centerId: '' }

  const nodeMap = new Map(nodes.map((n) => [n.id, n]))
  const adj = new Map<string, Set<string>>()
  nodes.forEach((n) => adj.set(n.id, new Set()))
  edges.forEach((e) => {
    adj.get(e.sourceId)?.add(e.targetId)
    adj.get(e.targetId)?.add(e.sourceId)
  })

  let centerId = nodes[0].id
  let maxDeg = -1
  nodes.forEach((n) => {
    const deg = n.degree ?? adj.get(n.id)?.size ?? 0
    if (deg > maxDeg) {
      maxDeg = deg
      centerId = n.id
    }
  })

  const layers = new Map<string, number>()
  const queue = [centerId]
  layers.set(centerId, 0)
  for (let i = 0; i < queue.length; i++) {
    const cur = queue[i]
    const neighbors = adj.get(cur) ?? new Set()
    for (const nbr of neighbors) {
      if (!layers.has(nbr)) {
        layers.set(nbr, layers.get(cur)! + 1)
        queue.push(nbr)
      }
    }
  }

  let maxLayer = 0
  nodes.forEach((n) => {
    if (!layers.has(n.id)) {
      layers.set(n.id, 3)
    }
    maxLayer = Math.max(maxLayer, layers.get(n.id)!)
  })

  const layerGroups = new Map<number, string[]>()
  nodes.forEach((n) => {
    const l = layers.get(n.id)!
    if (!layerGroups.has(l)) layerGroups.set(l, [])
    layerGroups.get(l)!.push(n.id)
  })

  const cx = width / 2
  const cy = height / 2
  const maxR = Math.min(width, height) / 2 - 60

  const positions = new Map<string, { x: number; y: number }>()
  positions.set(centerId, { x: cx, y: cy })

  for (let l = 1; l <= maxLayer; l++) {
    const ids = layerGroups.get(l) ?? []
    const r = maxLayer > 0 ? (l / maxLayer) * maxR : maxR
    ids.sort((a, b) =>
      (nodeMap.get(a)!.type ?? '').localeCompare(nodeMap.get(b)!.type ?? '')
    )
    ids.forEach((id, i) => {
      const angle = (i / Math.max(ids.length, 1)) * 2 * Math.PI - Math.PI / 2
      positions.set(id, {
        x: cx + r * Math.cos(angle),
        y: cy + r * Math.sin(angle),
      })
    })
  }

  return { positions, centerId }
}

export function GraphPreview({ data, onNodeSelect, selectedNodeId }: GraphPreviewProps) {
  const [hoveredNodeId, setHoveredNodeId] = useState<string | null>(null)
  const [tooltip, setTooltip] = useState<{
    x: number
    y: number
    node: GraphPreviewNode
  } | null>(null)

  const width = 800
  const height = 520

  const { positions, centerId } = useMemo(
    () => computeLayout(data, width, height),
    [data]
  )

  const nodeMap = useMemo(
    () => new Map(data.nodes.map((n) => [n.id, n])),
    [data.nodes]
  )

  const relatedMap = useMemo(() => {
    const map = new Map<string, Set<string>>()
    data.nodes.forEach((n) => map.set(n.id, new Set([n.id])))
    data.edges.forEach((e) => {
      map.get(e.sourceId)?.add(e.targetId)
      map.get(e.targetId)?.add(e.sourceId)
    })
    return map
  }, [data])

  const handleNodeEnter = useCallback(
    (e: React.MouseEvent, nodeId: string) => {
      setHoveredNodeId(nodeId)
      const node = nodeMap.get(nodeId)
      if (node) {
        setTooltip({ x: e.clientX + 12, y: e.clientY - 12, node })
      }
    },
    [nodeMap]
  )

  const handleNodeMove = useCallback((e: React.MouseEvent) => {
    setTooltip((prev) =>
      prev ? { ...prev, x: e.clientX + 12, y: e.clientY - 12 } : null
    )
  }, [])

  const handleNodeLeave = useCallback(() => {
    setHoveredNodeId(null)
    setTooltip(null)
  }, [])

  const handleNodeClick = useCallback(
    (nodeId: string) => {
      const node = nodeMap.get(nodeId) ?? null
      onNodeSelect?.(selectedNodeId === nodeId ? null : node)
    },
    [nodeMap, onNodeSelect, selectedNodeId]
  )

  const types = useMemo(() => {
    const set = new Set<string>()
    data.nodes.forEach((n) => set.add(n.type))
    return Array.from(set).sort()
  }, [data.nodes])

  const highlightNodeId = selectedNodeId || hoveredNodeId
  const highlightedRelated = highlightNodeId
    ? relatedMap.get(highlightNodeId) ?? new Set<string>()
    : new Set<string>()

  return (
    <div className="relative w-full">
      <svg
        viewBox={`0 0 ${width} ${height}`}
        className="w-full h-auto select-none"
        style={{ minHeight: 280 }}
      >
        <defs>
          <pattern id="grid" width="40" height="40" patternUnits="userSpaceOnUse">
            <path
              d="M 40 0 L 0 0 0 40"
              fill="none"
              stroke="#f1f5f9"
              strokeWidth="1"
            />
          </pattern>
        </defs>
        <rect width={width} height={height} fill="url(#grid)" rx="8" />

        {/* Edges */}
        <g>
          {data.edges.map((edge) => {
            const src = positions.get(edge.sourceId)
            const tgt = positions.get(edge.targetId)
            if (!src || !tgt) return null
            const isHighlighted =
              highlightNodeId &&
              (edge.sourceId === highlightNodeId ||
                edge.targetId === highlightNodeId)
            const isDimmed = highlightNodeId && !isHighlighted
            return (
              <line
                key={edge.id}
                x1={src.x}
                y1={src.y}
                x2={tgt.x}
                y2={tgt.y}
                stroke={isHighlighted ? '#94a3b8' : '#e2e8f0'}
                strokeWidth={isHighlighted ? 2.5 : 1}
                opacity={isDimmed ? 0.15 : 1}
                className="transition-all duration-200"
              />
            )
          })}
        </g>

        {/* Nodes */}
        <g>
          {data.nodes.map((node) => {
            const pos = positions.get(node.id)
            if (!pos) return null
            const isSelected = selectedNodeId === node.id
            const isHovered = hoveredNodeId === node.id
            const isRelated = highlightedRelated.has(node.id)
            const isDimmed = highlightNodeId && !isRelated
            const color = getTypeColor(node.type)
            const r = node.id === centerId ? 10 : 7
            const showLabel = isSelected || isHovered || node.id === centerId

            return (
              <g
                key={node.id}
                transform={`translate(${pos.x}, ${pos.y})`}
                onMouseEnter={(e) => handleNodeEnter(e, node.id)}
                onMouseMove={handleNodeMove}
                onMouseLeave={handleNodeLeave}
                onClick={() => handleNodeClick(node.id)}
                className="cursor-pointer"
                opacity={isDimmed ? 0.2 : 1}
              >
                {(isSelected || isHovered) && (
                  <circle
                    r={r + 6}
                    fill={color}
                    opacity={0.12}
                    className="transition-all"
                  />
                )}
                <circle
                  r={r}
                  fill={color}
                  stroke="#fff"
                  strokeWidth={isSelected ? 3 : 2}
                  className="transition-all duration-200"
                />
                {showLabel && (
                  <text
                    y={r + 14}
                    textAnchor="middle"
                    fontSize={11}
                    fill="#475569"
                    fontWeight={500}
                    className="pointer-events-none select-none"
                    style={{
                      textShadow:
                        '0 1px 2px rgba(255,255,255,0.95), 0 0 4px rgba(255,255,255,0.8)',
                    }}
                  >
                    {node.name.length > 10
                      ? node.name.slice(0, 10) + '...'
                      : node.name}
                  </text>
                )}
              </g>
            )
          })}
        </g>
      </svg>

      {/* Legend */}
      <div className="absolute top-3 right-3 bg-white/90 backdrop-blur-sm rounded-lg border border-gray-200 shadow-sm px-3 py-2.5 max-w-[200px]">
        <p className="text-xs font-semibold text-gray-500 mb-1.5">节点类型</p>
        <div className="flex flex-wrap gap-x-3 gap-y-1.5">
          {types.map((type) => (
            <div key={type} className="flex items-center gap-1">
              <span
                className="w-2 h-2 rounded-full"
                style={{ backgroundColor: getTypeColor(type) }}
              />
              <span className="text-[11px] text-gray-600">{type}</span>
            </div>
          ))}
        </div>
      </div>

      {/* Tooltip */}
      {tooltip && !selectedNodeId && (
        <div
          className="fixed z-50 pointer-events-none bg-gray-900 text-white text-xs rounded-lg px-3 py-2 shadow-xl"
          style={{
            left: Math.min(tooltip.x, window.innerWidth - 160),
            top: Math.max(tooltip.y, 8),
          }}
        >
          <p className="font-semibold">{tooltip.node.name}</p>
          <p className="text-gray-300 mt-0.5">{tooltip.node.type}</p>
          {tooltip.node.degree !== undefined && (
            <p className="text-gray-400 mt-0.5">连接数: {tooltip.node.degree}</p>
          )}
        </div>
      )}
    </div>
  )
}
