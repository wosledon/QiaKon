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
  文档: '#2563eb',
  章节: '#7c3aed',
  片段: '#059669',
  Platform: '#334155',
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

  const positions = new Map<string, { x: number; y: number }>()
  const centerX = width / 2
  const centerY = height / 2
  const radius = Math.min(width, height) * 0.34
  const nodeCount = Math.max(nodes.length, 1)
  const sortedNodeIds = [...nodes]
    .sort((a, b) => (b.degree ?? 0) - (a.degree ?? 0) || a.name.localeCompare(b.name))
    .map((node) => node.id)

  sortedNodeIds.forEach((id, index) => {
    if (id === centerId) {
      positions.set(id, { x: centerX, y: centerY })
      return
    }

    const angle = (index / Math.max(nodeCount - 1, 1)) * Math.PI * 2 - Math.PI / 2
    const ring = 0.45 + (index % 5) * 0.12
    positions.set(id, {
      x: centerX + Math.cos(angle) * radius * ring,
      y: centerY + Math.sin(angle) * radius * ring,
    })
  })

  for (let iteration = 0; iteration < 180; iteration += 1) {
    const forces = new Map<string, { x: number; y: number }>()
    sortedNodeIds.forEach((id) => forces.set(id, { x: 0, y: 0 }))

    for (let i = 0; i < sortedNodeIds.length; i += 1) {
      for (let j = i + 1; j < sortedNodeIds.length; j += 1) {
        const sourceId = sortedNodeIds[i]
        const targetId = sortedNodeIds[j]
        const source = positions.get(sourceId)
        const target = positions.get(targetId)
        if (!source || !target) continue

        let dx = target.x - source.x
        let dy = target.y - source.y
        let distanceSquared = dx * dx + dy * dy
        if (distanceSquared < 0.01) {
          dx = (i % 3) - 1
          dy = (j % 3) - 1
          distanceSquared = dx * dx + dy * dy
        }

        const distance = Math.sqrt(distanceSquared)
        const repulsion = 4200 / distanceSquared
        const fx = (dx / distance) * repulsion
        const fy = (dy / distance) * repulsion

        forces.get(sourceId)!.x -= fx
        forces.get(sourceId)!.y -= fy
        forces.get(targetId)!.x += fx
        forces.get(targetId)!.y += fy
      }
    }

    edges.forEach((edge) => {
      const source = positions.get(edge.sourceId)
      const target = positions.get(edge.targetId)
      if (!source || !target) return

      const dx = target.x - source.x
      const dy = target.y - source.y
      const distance = Math.max(Math.sqrt(dx * dx + dy * dy), 1)
      const desiredDistance = 84
      const attraction = (distance - desiredDistance) * 0.014
      const fx = (dx / distance) * attraction
      const fy = (dy / distance) * attraction

      forces.get(edge.sourceId)!.x += fx
      forces.get(edge.sourceId)!.y += fy
      forces.get(edge.targetId)!.x -= fx
      forces.get(edge.targetId)!.y -= fy
    })

    sortedNodeIds.forEach((id) => {
      const position = positions.get(id)
      const force = forces.get(id)
      if (!position || !force) return

      const node = nodeMap.get(id)
      const centerPull = id === centerId ? 0.08 : 0.018
      force.x += (centerX - position.x) * centerPull
      force.y += (centerY - position.y) * centerPull

      const damping = id === centerId ? 0.02 : 0.08
      position.x = clamp(position.x + force.x * damping, 48, width - 48)
      position.y = clamp(position.y + force.y * damping, 48, height - 48)

      if (node?.degree === 0 && id !== centerId) {
        position.x = clamp(position.x + Math.cos(iteration + position.y) * 0.4, 48, width - 48)
        position.y = clamp(position.y + Math.sin(iteration + position.x) * 0.4, 48, height - 48)
      }
    })
  }

  return { positions, centerId }
}

function clamp(value: number, min: number, max: number) {
  return Math.min(Math.max(value, min), max)
}

export function GraphPreview({ data, onNodeSelect, selectedNodeId }: GraphPreviewProps) {
  const [hoveredNodeId, setHoveredNodeId] = useState<string | null>(null)
  const [tooltip, setTooltip] = useState<{
    x: number
    y: number
    node: GraphPreviewNode
  } | null>(null)

  const width = 1040
  const height = 680

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
  const emphasizedNodeIds = useMemo(() => {
    return new Set(
      [...data.nodes]
        .sort((a, b) => (b.degree ?? 0) - (a.degree ?? 0))
        .slice(0, Math.min(10, data.nodes.length))
        .map((node) => node.id)
    )
  }, [data.nodes])

  return (
    <div className="relative w-full">
      <svg
        viewBox={`0 0 ${width} ${height}`}
        className="min-h-[280px] h-auto w-full select-none"
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
        <rect width={width} height={height} fill="url(#grid)" rx="18" />

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
                stroke={isHighlighted ? '#64748b' : '#dbe4f0'}
                strokeWidth={isHighlighted ? 2.8 : 1.25}
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
            const degree = node.degree ?? 0
            const r = node.id === centerId ? 14 : clamp(5 + degree * 0.18, 6, 10)
            const showLabel = isSelected || isHovered || node.id === centerId || emphasizedNodeIds.has(node.id)

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
                    r={r + 8}
                    fill={color}
                    opacity={0.16}
                    className="transition-all"
                  />
                )}
                <circle
                  r={r}
                  fill={color}
                  stroke={isSelected ? '#0f172a' : '#fff'}
                  strokeWidth={isSelected ? 3.2 : 2}
                  className="transition-all duration-200"
                />
                {showLabel && (
                  <>
                    <rect
                      x={-44}
                      y={r + 7}
                      rx={8}
                      width={88}
                      height={18}
                      fill="rgba(255,255,255,0.9)"
                      stroke="rgba(226,232,240,0.9)"
                    />
                    <text
                      y={r + 20}
                      textAnchor="middle"
                      fontSize={11}
                      fill="#334155"
                      fontWeight={600}
                      className="pointer-events-none select-none"
                    >
                      {node.name.length > 11
                        ? node.name.slice(0, 11) + '…'
                        : node.name}
                    </text>
                  </>
                )}
              </g>
            )
          })}
        </g>

        {tooltip && !selectedNodeId && positions.get(tooltip.node.id) && (() => {
          const tooltipPosition = positions.get(tooltip.node.id)!
          const tooltipX = clamp(tooltipPosition.x + 18, 16, width - 176)
          const tooltipY = clamp(tooltipPosition.y - 56, 16, height - 64)

          return (
            <g transform={`translate(${tooltipX}, ${tooltipY})`} pointerEvents="none">
              <rect width="160" height="52" rx="10" fill="#0f172a" opacity="0.96" />
              <text x="12" y="18" fontSize="12" fontWeight="700" fill="#ffffff">
                {tooltip.node.name.length > 18 ? `${tooltip.node.name.slice(0, 18)}…` : tooltip.node.name}
              </text>
              <text x="12" y="33" fontSize="11" fill="#cbd5e1">
                {tooltip.node.type}
              </text>
              {tooltip.node.degree !== undefined && (
                <text x="12" y="47" fontSize="11" fill="#94a3b8">
                  连接数: {tooltip.node.degree}
                </text>
              )}
            </g>
          )
        })()}
      </svg>

      {/* Legend */}
      <div className="absolute top-3 right-3 bg-white/92 backdrop-blur-sm rounded-lg border border-gray-200 shadow-sm px-3 py-2.5 max-w-[220px]">
        <p className="text-xs font-semibold text-gray-500 mb-1.5">节点类型</p>
        <div className="flex flex-wrap gap-x-3 gap-y-1.5">
          {types.map((type) => (
            <div key={type} className="flex items-center gap-1">
              <svg className="h-2 w-2" viewBox="0 0 8 8" aria-hidden="true">
                <circle cx="4" cy="4" r="4" fill={getTypeColor(type)} />
              </svg>
              <span className="text-[11px] text-gray-600">{type}</span>
            </div>
          ))}
        </div>
      </div>
    </div>
  )
}
