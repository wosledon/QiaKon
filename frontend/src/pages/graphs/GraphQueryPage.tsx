import { useState, useCallback } from 'react'
import { useNavigate } from 'react-router-dom'
import { PageHeader } from '@/components/shared/PageHeader'
import { Button } from '@/components/ui/Button'
import { Card, CardHeader, CardContent } from '@/components/ui/Card'
import { Input } from '@/components/ui/Input'
import { Select } from '@/components/ui/Select'
import { graphApi } from '@/services/api'
import type { GraphEntity, GraphPath, GraphNeighbor, GraphAggregateItem } from '@/types'
import { Search, Route, Layers, Share2, BarChart3 } from 'lucide-react'

type QueryTab = 'path' | 'multiHop' | 'neighbor' | 'aggregate'

const tabOptions = [
  { id: 'path' as QueryTab, label: '路径查询', icon: <Route className="w-4 h-4" /> },
  { id: 'multiHop' as QueryTab, label: '多跳推理', icon: <Layers className="w-4 h-4" /> },
  { id: 'neighbor' as QueryTab, label: '邻居查询', icon: <Share2 className="w-4 h-4" /> },
  { id: 'aggregate' as QueryTab, label: '聚合查询', icon: <BarChart3 className="w-4 h-4" /> },
]

const directionOptions = [
  { value: 'out', label: '出度' },
  { value: 'in', label: '入度' },
  { value: 'both', label: '双向' },
]

const groupByOptions = [
  { value: 'type', label: '按类型' },
  { value: 'department', label: '按部门' },
]

export function GraphQueryPage() {
  const navigate = useNavigate()
  const [activeTab, setActiveTab] = useState<QueryTab>('path')
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState('')

  // Path query state
  const [sourceQuery, setSourceQuery] = useState('')
  const [targetQuery, setTargetQuery] = useState('')
  const [sourceResults, setSourceResults] = useState<GraphEntity[]>([])
  const [targetResults, setTargetResults] = useState<GraphEntity[]>([])
  const [sourceId, setSourceId] = useState('')
  const [targetId, setTargetId] = useState('')
  const [maxPaths, setMaxPaths] = useState(5)
  const [paths, setPaths] = useState<GraphPath[]>([])
  const [showSourceDropdown, setShowSourceDropdown] = useState(false)
  const [showTargetDropdown, setShowTargetDropdown] = useState(false)

  // Multi-hop state
  const [hopQuery, setHopQuery] = useState('')
  const [hopResults, setHopResults] = useState<GraphEntity[]>([])
  const [hopEntityId, setHopEntityId] = useState('')
  const [maxHops, setMaxHops] = useState(3)
  const [hopEntities, setHopEntities] = useState<GraphEntity[]>([])
  const [showHopDropdown, setShowHopDropdown] = useState(false)

  // Neighbor state
  const [neighborQuery, setNeighborQuery] = useState('')
  const [neighborResults, setNeighborResults] = useState<GraphEntity[]>([])
  const [neighborEntityId, setNeighborEntityId] = useState('')
  const [neighborDirection, setNeighborDirection] = useState<'out' | 'in' | 'both'>('both')
  const [neighbors, setNeighbors] = useState<GraphNeighbor[]>([])
  const [showNeighborDropdown, setShowNeighborDropdown] = useState(false)

  // Aggregate state
  const [groupBy, setGroupBy] = useState<'type' | 'department'>('type')
  const [aggFilterType, setAggFilterType] = useState('')
  const [aggFilterDept, setAggFilterDept] = useState('')
  const [aggResults, setAggResults] = useState<GraphAggregateItem[]>([])

  const searchEntities = useCallback(async (keyword: string, setResults: (r: GraphEntity[]) => void) => {
    if (!keyword.trim()) {
      setResults([])
      return
    }
    try {
      const data = await graphApi.searchEntities(keyword.trim())
      setResults(data)
    } catch {
      setResults([])
    }
  }, [])

  const handleQueryPath = async () => {
    if (!sourceId || !targetId) {
      setError('请选择源实体和目标实体')
      return
    }
    setIsLoading(true)
    setError('')
    try {
      const data = await graphApi.queryPath({ sourceId, targetId, maxPaths })
      setPaths(data.paths)
    } catch (err) {
      setError(err instanceof Error ? err.message : '查询失败')
    } finally {
      setIsLoading(false)
    }
  }

  const handleQueryMultiHop = async () => {
    if (!hopEntityId) {
      setError('请选择起始实体')
      return
    }
    setIsLoading(true)
    setError('')
    try {
      const data = await graphApi.queryMultiHop({ startEntityId: hopEntityId, maxHops })
      setHopEntities(data.entities)
    } catch (err) {
      setError(err instanceof Error ? err.message : '查询失败')
    } finally {
      setIsLoading(false)
    }
  }

  const handleQueryNeighbors = async () => {
    if (!neighborEntityId) {
      setError('请选择实体')
      return
    }
    setIsLoading(true)
    setError('')
    try {
      const data = await graphApi.queryNeighbors({ entityId: neighborEntityId, direction: neighborDirection })
      setNeighbors(data.neighbors)
    } catch (err) {
      setError(err instanceof Error ? err.message : '查询失败')
    } finally {
      setIsLoading(false)
    }
  }

  const handleQueryAggregate = async () => {
    setIsLoading(true)
    setError('')
    try {
      const data = await graphApi.queryAggregate({
        groupBy,
        filterType: aggFilterType || undefined,
        filterDepartment: aggFilterDept || undefined,
      })
      setAggResults(data.results)
    } catch (err) {
      setError(err instanceof Error ? err.message : '查询失败')
    } finally {
      setIsLoading(false)
    }
  }

  const renderEntitySearch = (
    query: string,
    onQueryChange: (v: string) => void,
    results: GraphEntity[],
    showDropdown: boolean,
    setShowDropdown: (v: boolean) => void,
    onSelect: (entity: GraphEntity) => void,
    placeholder: string
  ) => (
    <div className="relative">
      <Input
        value={query}
        onChange={(e) => {
          onQueryChange(e.target.value)
          setShowDropdown(true)
          searchEntities(e.target.value, () => {})
        }}
        onFocus={() => {
          setShowDropdown(true)
          if (query) searchEntities(query, () => {})
        }}
        placeholder={placeholder}
      />
      {showDropdown && results.length > 0 && (
        <div className="absolute z-10 mt-1 w-full bg-white border border-gray-200 rounded-lg shadow-lg max-h-48 overflow-y-auto">
          {results.map((entity) => (
            <button
              key={entity.id}
              className="w-full text-left px-4 py-2 hover:bg-gray-50 text-sm"
              onClick={() => {
                onSelect(entity)
                setShowDropdown(false)
              }}
            >
              <span className="font-medium text-gray-900">{entity.name}</span>
              <span className="ml-2 text-xs text-gray-500">{entity.type}</span>
            </button>
          ))}
        </div>
      )}
    </div>
  )

  return (
    <div className="p-4 md:p-8 max-w-6xl mx-auto">
      <PageHeader title="图谱查询" description="支持路径查询、多跳推理、邻居查询和聚合查询" />

      {/* Tabs */}
      <div className="flex items-center gap-1 mb-6 bg-gray-100 p-1 rounded-lg w-fit">
        {tabOptions.map((tab) => (
          <button
            key={tab.id}
            onClick={() => {
              setActiveTab(tab.id)
              setError('')
              setPaths([])
              setHopEntities([])
              setNeighbors([])
              setAggResults([])
            }}
            className={`flex items-center gap-1.5 px-4 py-2 text-sm font-medium rounded-md transition-colors ${
              activeTab === tab.id
                ? 'bg-white text-gray-900 shadow-sm'
                : 'text-gray-500 hover:text-gray-700'
            }`}
          >
            {tab.icon}
            {tab.label}
          </button>
        ))}
      </div>

      {error && (
        <div className="mb-4 rounded-lg bg-red-50 px-4 py-3 text-sm text-red-600">{error}</div>
      )}

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <div className="lg:col-span-1 space-y-4">
          {activeTab === 'path' && (
            <Card>
              <CardHeader>
                <h3 className="text-base font-semibold text-gray-900">路径查询</h3>
              </CardHeader>
              <CardContent className="space-y-4">
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">源实体</label>
                  {renderEntitySearch(
                    sourceQuery,
                    (v) => {
                      setSourceQuery(v)
                      searchEntities(v, setSourceResults)
                    },
                    sourceResults,
                    showSourceDropdown,
                    setShowSourceDropdown,
                    (e) => {
                      setSourceId(e.id)
                      setSourceQuery(e.name)
                    },
                    '搜索源实体'
                  )}
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">目标实体</label>
                  {renderEntitySearch(
                    targetQuery,
                    (v) => {
                      setTargetQuery(v)
                      searchEntities(v, setTargetResults)
                    },
                    targetResults,
                    showTargetDropdown,
                    setShowTargetDropdown,
                    (e) => {
                      setTargetId(e.id)
                      setTargetQuery(e.name)
                    },
                    '搜索目标实体'
                  )}
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">最大路径数</label>
                  <Input
                    type="number"
                    min={1}
                    max={20}
                    value={maxPaths}
                    onChange={(e) => setMaxPaths(Number(e.target.value))}
                  />
                </div>
                <Button onClick={handleQueryPath} isLoading={isLoading} className="w-full">
                  <Search className="w-4 h-4 mr-1" />
                  查询路径
                </Button>
              </CardContent>
            </Card>
          )}

          {activeTab === 'multiHop' && (
            <Card>
              <CardHeader>
                <h3 className="text-base font-semibold text-gray-900">多跳推理</h3>
              </CardHeader>
              <CardContent className="space-y-4">
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">起始实体</label>
                  {renderEntitySearch(
                    hopQuery,
                    (v) => {
                      setHopQuery(v)
                      searchEntities(v, setHopResults)
                    },
                    hopResults,
                    showHopDropdown,
                    setShowHopDropdown,
                    (e) => {
                      setHopEntityId(e.id)
                      setHopQuery(e.name)
                    },
                    '搜索起始实体'
                  )}
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">最大跳数</label>
                  <Input
                    type="number"
                    min={1}
                    max={10}
                    value={maxHops}
                    onChange={(e) => setMaxHops(Number(e.target.value))}
                  />
                </div>
                <Button onClick={handleQueryMultiHop} isLoading={isLoading} className="w-full">
                  <Search className="w-4 h-4 mr-1" />
                  查询可达实体
                </Button>
              </CardContent>
            </Card>
          )}

          {activeTab === 'neighbor' && (
            <Card>
              <CardHeader>
                <h3 className="text-base font-semibold text-gray-900">邻居查询</h3>
              </CardHeader>
              <CardContent className="space-y-4">
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">实体</label>
                  {renderEntitySearch(
                    neighborQuery,
                    (v) => {
                      setNeighborQuery(v)
                      searchEntities(v, setNeighborResults)
                    },
                    neighborResults,
                    showNeighborDropdown,
                    setShowNeighborDropdown,
                    (e) => {
                      setNeighborEntityId(e.id)
                      setNeighborQuery(e.name)
                    },
                    '搜索实体'
                  )}
                </div>
                <Select
                  label="方向"
                  value={neighborDirection}
                  onChange={(e) => setNeighborDirection(e.target.value as 'out' | 'in' | 'both')}
                  options={directionOptions}
                />
                <Button onClick={handleQueryNeighbors} isLoading={isLoading} className="w-full">
                  <Search className="w-4 h-4 mr-1" />
                  查询邻居
                </Button>
              </CardContent>
            </Card>
          )}

          {activeTab === 'aggregate' && (
            <Card>
              <CardHeader>
                <h3 className="text-base font-semibold text-gray-900">聚合查询</h3>
              </CardHeader>
              <CardContent className="space-y-4">
                <Select
                  label="分组方式"
                  value={groupBy}
                  onChange={(e) => setGroupBy(e.target.value as 'type' | 'department')}
                  options={groupByOptions}
                />
                <Input
                  label="筛选类型（可选）"
                  value={aggFilterType}
                  onChange={(e) => setAggFilterType(e.target.value)}
                  placeholder="输入类型"
                />
                <Input
                  label="筛选部门（可选）"
                  value={aggFilterDept}
                  onChange={(e) => setAggFilterDept(e.target.value)}
                  placeholder="输入部门"
                />
                <Button onClick={handleQueryAggregate} isLoading={isLoading} className="w-full">
                  <Search className="w-4 h-4 mr-1" />
                  查询统计
                </Button>
              </CardContent>
            </Card>
          )}
        </div>

        <div className="lg:col-span-2">
          <Card className="h-full">
            <CardHeader>
              <h3 className="text-base font-semibold text-gray-900">查询结果</h3>
            </CardHeader>
            <CardContent>
              {isLoading ? (
                <div className="flex items-center justify-center py-16 text-gray-400">
                  <svg className="animate-spin h-8 w-8 mr-3" fill="none" viewBox="0 0 24 24">
                    <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                    <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
                  </svg>
                  查询中...
                </div>
              ) : activeTab === 'path' && paths.length > 0 ? (
                <div className="space-y-4">
                  {paths.map((path, idx) => (
                    <div key={idx} className="bg-gray-50 rounded-lg p-4">
                      <p className="text-sm font-medium text-gray-700 mb-2">路径 {idx + 1}</p>
                      <div className="flex flex-wrap items-center gap-1">
                        {path.nodes.map((node, nidx) => (
                          <>
                            <button
                              key={node.id}
                              onClick={() => navigate(`/graphs/entities/${node.id}`)}
                              className="text-sm font-medium text-blue-600 hover:text-blue-800 px-2 py-1 bg-white rounded border border-blue-100"
                            >
                              {node.name}
                            </button>
                            {nidx < path.edges.length && (
                              <span className="text-xs text-gray-400 px-1">
                                — {path.edges[nidx]?.type} →
                              </span>
                            )}
                          </>
                        ))}
                      </div>
                    </div>
                  ))}
                </div>
              ) : activeTab === 'multiHop' && hopEntities.length > 0 ? (
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                  {hopEntities.map((entity) => (
                    <button
                      key={entity.id}
                      onClick={() => navigate(`/graphs/entities/${entity.id}`)}
                      className="text-left px-4 py-3 bg-gray-50 rounded-lg hover:bg-gray-100 transition-colors"
                    >
                      <p className="text-sm font-medium text-gray-900">{entity.name}</p>
                      <p className="text-xs text-gray-500">{entity.type}</p>
                    </button>
                  ))}
                </div>
              ) : activeTab === 'neighbor' && neighbors.length > 0 ? (
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                  {neighbors.map((n, idx) => (
                    <button
                      key={`${n.entity.id}-${idx}`}
                      onClick={() => navigate(`/graphs/entities/${n.entity.id}`)}
                      className="text-left px-4 py-3 bg-gray-50 rounded-lg hover:bg-gray-100 transition-colors"
                    >
                      <p className="text-sm font-medium text-gray-900">{n.entity.name}</p>
                      <p className="text-xs text-gray-500">
                        {n.relationType} · {n.direction === 'out' ? '出边' : '入边'}
                      </p>
                    </button>
                  ))}
                </div>
              ) : activeTab === 'aggregate' && aggResults.length > 0 ? (
                <div className="space-y-3">
                  {aggResults.map((item) => (
                    <div key={item.key} className="flex items-center gap-3">
                      <span className="text-sm text-gray-600 w-24">{item.key}</span>
                      <div className="flex-1 bg-gray-100 rounded-full h-2">
                        <div
                          className="bg-emerald-500 h-2 rounded-full"
                          style={{
                            width: `${Math.min(
                              (item.count / Math.max(...aggResults.map((r) => r.count))) * 100,
                              100
                            )}%`,
                          }}
                        />
                      </div>
                      <span className="text-sm font-medium text-gray-900 w-12 text-right">{item.count}</span>
                    </div>
                  ))}
                </div>
              ) : (
                <div className="text-center py-16 text-gray-400">
                  <p>请输入查询条件并点击查询按钮</p>
                </div>
              )}
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  )
}
