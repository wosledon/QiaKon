import { useState, useEffect, useCallback } from 'react'
import { Card, CardHeader, CardContent } from '@/components/ui/Card'
import { Input } from '@/components/ui/Input'
import { graphApi } from '@/services/api'
import type { GraphEntity } from '@/types'

export function GraphPage() {
  const [entities, setEntities] = useState<GraphEntity[]>([])
  const [filtered, setFiltered] = useState<GraphEntity[]>([])
  const [search, setSearch] = useState('')
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState('')

  const loadEntities = useCallback(async () => {
    setIsLoading(true)
    setError('')
    try {
      const data = await graphApi.entities()
      setEntities(data)
      setFiltered(data)
    } catch (err) {
      setError(err instanceof Error ? err.message : '加载失败')
    } finally {
      setIsLoading(false)
    }
  }, [])

  useEffect(() => {
    loadEntities()
  }, [loadEntities])

  useEffect(() => {
    const term = search.trim().toLowerCase()
    if (!term) {
      setFiltered(entities)
      return
    }
    setFiltered(
      entities.filter(
        (e) =>
          e.name.toLowerCase().includes(term) ||
          e.type.toLowerCase().includes(term)
      )
    )
  }, [search, entities])

  return (
    <div className="p-4 md:p-8 max-w-6xl mx-auto">
      <h1 className="text-2xl font-bold text-gray-900 mb-6">知识图谱</h1>

      <div className="mb-6 max-w-md">
        <Input
          placeholder="搜索实体..."
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
      </div>

      {error && (
        <div className="mb-4 rounded-lg bg-red-50 px-4 py-3 text-sm text-red-600">{error}</div>
      )}

      {isLoading ? (
        <div className="flex items-center justify-center py-16 text-gray-400">
          <svg className="animate-spin h-8 w-8 mr-3" fill="none" viewBox="0 0 24 24">
            <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
            <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
          </svg>
          加载中...
        </div>
      ) : filtered.length === 0 ? (
        <div className="text-center py-16 text-gray-400">
          <svg className="w-16 h-16 mx-auto mb-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M7 20l4-16m2 16l4-16M6 9h14M4 15h14" />
          </svg>
          <p className="text-lg">暂无实体</p>
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          {filtered.map((entity) => (
            <Card key={entity.id} className="hover:shadow-md transition-shadow">
              <CardHeader>
                <div className="flex items-center justify-between">
                  <h3 className="font-semibold text-gray-900 truncate">{entity.name}</h3>
                  <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-blue-50 text-blue-700 flex-shrink-0 ml-2">
                    {entity.type}
                  </span>
                </div>
              </CardHeader>
              <CardContent>
                <div className="space-y-2">
                  {Object.entries(entity.properties).slice(0, 4).map(([key, value]) => (
                    <div key={key} className="flex items-start gap-2 text-sm">
                      <span className="text-gray-500 flex-shrink-0">{key}:</span>
                      <span className="text-gray-800 truncate">{String(value)}</span>
                    </div>
                  ))}
                  {Object.keys(entity.properties).length > 4 && (
                    <p className="text-xs text-gray-400">
                      +{Object.keys(entity.properties).length - 4} 个属性
                    </p>
                  )}
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}
    </div>
  )
}
