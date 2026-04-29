import { useState, useEffect, useCallback } from 'react'
import { Card, CardHeader, CardContent } from '@/components/ui/Card'
import { Input } from '@/components/ui/Input'
import { Button } from '@/components/ui/Button'
import { Dialog } from '@/components/ui/Dialog'
import { Textarea } from '@/components/ui/Textarea'
import { graphApi } from '@/services/api'
import type { GraphEntity, GraphRelation, GraphPath } from '@/types'

type TabId = 'entities' | 'relations' | 'query'

export function GraphPage() {
  const [activeTab, setActiveTab] = useState<TabId>('entities')

  const [entities, setEntities] = useState<GraphEntity[]>([])
  const [relations, setRelations] = useState<GraphRelation[]>([])
  const [entitySearch, setEntitySearch] = useState('')
  const [relationSearch, setRelationSearch] = useState('')

  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState('')

  const [entityModalOpen, setEntityModalOpen] = useState(false)
  const [relationModalOpen, setRelationModalOpen] = useState(false)

  const [entityForm, setEntityForm] = useState({ name: '', type: '', properties: '' })
  const [relationForm, setRelationForm] = useState({ source: '', target: '', type: '', properties: '' })
  const [formError, setFormError] = useState('')
  const [formSubmitting, setFormSubmitting] = useState(false)

  const [queryForm, setQueryForm] = useState({ startEntity: '', endEntity: '', relationType: '', maxDepth: '2' })
  const [queryPaths, setQueryPaths] = useState<GraphPath[]>([])
  const [queryLoading, setQueryLoading] = useState(false)
  const [queryError, setQueryError] = useState('')

  const loadEntities = useCallback(async () => {
    setIsLoading(true)
    setError('')
    try {
      const data = await graphApi.entities()
      setEntities(data.items)
    } catch (err) {
      setError(err instanceof Error ? err.message : '加载实体失败')
    } finally {
      setIsLoading(false)
    }
  }, [])

  const loadRelations = useCallback(async () => {
    setIsLoading(true)
    setError('')
    try {
      const data = await graphApi.relations()
      setRelations(data.items)
    } catch (err) {
      setError(err instanceof Error ? err.message : '加载关系失败')
    } finally {
      setIsLoading(false)
    }
  }, [])

  useEffect(() => {
    if (activeTab === 'entities') loadEntities()
    if (activeTab === 'relations') loadRelations()
  }, [activeTab, loadEntities, loadRelations])

  const filteredEntities = entities.filter(
    (e) =>
      e.name.toLowerCase().includes(entitySearch.trim().toLowerCase()) ||
      e.type.toLowerCase().includes(entitySearch.trim().toLowerCase())
  )

  const filteredRelations = relations.filter(
    (r) =>
      r.sourceName.toLowerCase().includes(relationSearch.trim().toLowerCase()) ||
      r.targetName.toLowerCase().includes(relationSearch.trim().toLowerCase()) ||
      r.type.toLowerCase().includes(relationSearch.trim().toLowerCase())
  )

  const handleCreateEntity = async () => {
    setFormError('')
    if (!entityForm.name.trim() || !entityForm.type.trim()) {
      setFormError('名称和类型为必填项')
      return
    }
    let properties: Record<string, unknown> = {}
    if (entityForm.properties.trim()) {
      try {
        properties = JSON.parse(entityForm.properties)
      } catch {
        setFormError('属性必须是合法 JSON')
        return
      }
    }
    setFormSubmitting(true)
    try {
      await graphApi.createEntity({
        name: entityForm.name.trim(),
        type: entityForm.type.trim(),
        properties,
      })
      setEntityModalOpen(false)
      setEntityForm({ name: '', type: '', properties: '' })
      await loadEntities()
    } catch (err) {
      setFormError(err instanceof Error ? err.message : '创建失败')
    } finally {
      setFormSubmitting(false)
    }
  }

  const handleCreateRelation = async () => {
    setFormError('')
    if (!relationForm.source.trim() || !relationForm.target.trim() || !relationForm.type.trim()) {
      setFormError('源实体、目标实体和类型为必填项')
      return
    }
    let properties: Record<string, unknown> = {}
    if (relationForm.properties.trim()) {
      try {
        properties = JSON.parse(relationForm.properties)
      } catch {
        setFormError('属性必须是合法 JSON')
        return
      }
    }
    setFormSubmitting(true)
    try {
      await graphApi.createRelation({
        sourceId: relationForm.source.trim(),
        targetId: relationForm.target.trim(),
        type: relationForm.type.trim(),
        properties,
      })
      setRelationModalOpen(false)
      setRelationForm({ source: '', target: '', type: '', properties: '' })
      await loadRelations()
    } catch (err) {
      setFormError(err instanceof Error ? err.message : '创建失败')
    } finally {
      setFormSubmitting(false)
    }
  }

  const handleQuery = async () => {
    setQueryError('')
    setQueryLoading(true)
    try {
      if (!queryForm.startEntity.trim() || !queryForm.endEntity.trim()) {
        setQueryError('请输入起始实体和目标实体')
        setQueryLoading(false)
        return
      }
      const data = await graphApi.queryPath({
        sourceId: queryForm.startEntity.trim(),
        targetId: queryForm.endEntity.trim(),
        maxPaths: queryForm.maxDepth ? Number(queryForm.maxDepth) : 5,
      })
      setQueryPaths(data.paths)
    } catch (err) {
      setQueryError(err instanceof Error ? err.message : '查询失败')
    } finally {
      setQueryLoading(false)
    }
  }

  const tabs: { id: TabId; label: string }[] = [
    { id: 'entities', label: '实体' },
    { id: 'relations', label: '关系' },
    { id: 'query', label: '路径查询' },
  ]

  return (
    <div className="p-4 md:p-8 max-w-6xl mx-auto">
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4 mb-6">
        <h1 className="text-2xl font-bold text-gray-900">知识图谱</h1>
        <div className="flex items-center gap-2">
          {activeTab === 'entities' && (
            <Button size="sm" onClick={() => setEntityModalOpen(true)}>+ 新建实体</Button>
          )}
          {activeTab === 'relations' && (
            <Button size="sm" onClick={() => setRelationModalOpen(true)}>+ 新建关系</Button>
          )}
        </div>
      </div>

      {/* Tabs */}
      <div className="flex items-center gap-1 mb-6 border-b border-gray-200">
        {tabs.map((t) => (
          <button
            key={t.id}
            onClick={() => setActiveTab(t.id)}
            className={`px-4 py-2 text-sm font-medium border-b-2 transition-colors ${
              activeTab === t.id
                ? 'border-blue-600 text-blue-700'
                : 'border-transparent text-gray-500 hover:text-gray-700'
            }`}
          >
            {t.label}
          </button>
        ))}
      </div>

      {error && activeTab !== 'query' && (
        <div className="mb-4 rounded-lg bg-red-50 px-4 py-3 text-sm text-red-600">{error}</div>
      )}

      {/* Entities */}
      {activeTab === 'entities' && (
        <>
          <div className="mb-4 max-w-md">
            <Input
              placeholder="搜索实体名称或类型..."
              value={entitySearch}
              onChange={(e) => setEntitySearch(e.target.value)}
            />
          </div>
          {isLoading ? (
            <div className="flex items-center justify-center py-16 text-gray-400">加载中...</div>
          ) : filteredEntities.length === 0 ? (
            <div className="text-center py-16 text-gray-400">
              <p className="text-lg">暂无实体</p>
            </div>
          ) : (
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
              {filteredEntities.map((entity) => (
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
                      {Object.entries(entity.properties)
                        .slice(0, 4)
                        .map(([key, value]) => (
                          <div key={key} className="flex items-start gap-2 text-sm">
                            <span className="text-gray-500 flex-shrink-0">{key}:</span>
                            <span className="text-gray-800 truncate">{String(value)}</span>
                          </div>
                        ))}
                      {Object.keys(entity.properties).length > 4 && (
                        <p className="text-xs text-gray-400">+{Object.keys(entity.properties).length - 4} 个属性</p>
                      )}
                    </div>
                  </CardContent>
                </Card>
              ))}
            </div>
          )}
        </>
      )}

      {/* Relations */}
      {activeTab === 'relations' && (
        <>
          <div className="mb-4 max-w-md">
            <Input
              placeholder="搜索源实体、目标实体或关系类型..."
              value={relationSearch}
              onChange={(e) => setRelationSearch(e.target.value)}
            />
          </div>
          {isLoading ? (
            <div className="flex items-center justify-center py-16 text-gray-400">加载中...</div>
          ) : filteredRelations.length === 0 ? (
            <div className="text-center py-16 text-gray-400">
              <p className="text-lg">暂没关系</p>
            </div>
          ) : (
            <div className="grid gap-4">
              {filteredRelations.map((relation) => (
                <Card key={relation.id} className="p-4">
                  <div className="flex flex-col sm:flex-row sm:items-center gap-2 sm:gap-4">
                    <span className="inline-flex items-center px-2.5 py-1 rounded-md text-sm font-medium bg-gray-100 text-gray-800">
                      {relation.sourceName}
                    </span>
                    <span className="text-xs text-gray-400">→</span>
                    <span className="inline-flex items-center px-2.5 py-1 rounded-md text-sm font-medium bg-blue-50 text-blue-700">
                      {relation.type}
                    </span>
                    <span className="text-xs text-gray-400">→</span>
                    <span className="inline-flex items-center px-2.5 py-1 rounded-md text-sm font-medium bg-gray-100 text-gray-800">
                      {relation.targetName}
                    </span>
                  </div>
                  {relation.properties && Object.keys(relation.properties).length > 0 && (
                    <div className="mt-3 pt-3 border-t border-gray-100 flex flex-wrap gap-2">
                      {Object.entries(relation.properties).slice(0, 4).map(([k, v]) => (
                        <span key={k} className="text-xs bg-gray-50 text-gray-600 px-2 py-0.5 rounded">
                          {k}: {String(v)}
                        </span>
                      ))}
                    </div>
                  )}
                </Card>
              ))}
            </div>
          )}
        </>
      )}

      {/* Query */}
      {activeTab === 'query' && (
        <>
          <Card className="p-4 mb-6">
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <Input
                placeholder="起始实体"
                value={queryForm.startEntity}
                onChange={(e) => setQueryForm((s) => ({ ...s, startEntity: e.target.value }))}
              />
              <Input
                placeholder="目标实体"
                value={queryForm.endEntity}
                onChange={(e) => setQueryForm((s) => ({ ...s, endEntity: e.target.value }))}
              />
              <Input
                placeholder="关系类型（可选）"
                value={queryForm.relationType}
                onChange={(e) => setQueryForm((s) => ({ ...s, relationType: e.target.value }))}
              />
              <Input
                placeholder="最大深度"
                type="number"
                min={1}
                max={10}
                value={queryForm.maxDepth}
                onChange={(e) => setQueryForm((s) => ({ ...s, maxDepth: e.target.value }))}
              />
            </div>
            <div className="mt-4 flex items-center gap-3">
              <Button onClick={handleQuery} isLoading={queryLoading}>执行查询</Button>
              <Button
                variant="secondary"
                onClick={() => {
                  setQueryPaths([])
                  setQueryForm({ startEntity: '', endEntity: '', relationType: '', maxDepth: '2' })
                }}
              >
                重置
              </Button>
            </div>
            {queryError && (
              <div className="mt-3 rounded-lg bg-red-50 px-4 py-2 text-sm text-red-600">{queryError}</div>
            )}
          </Card>

          {queryPaths.length > 0 && (
            <div className="space-y-4">
              <h3 className="text-sm font-semibold text-gray-700">查询结果（{queryPaths.length} 条路径）</h3>
              {queryPaths.map((path, idx) => (
                <Card key={idx} className="p-4">
                  <div className="flex flex-wrap items-center gap-2">
                    {path.nodes.map((node, nIdx) => (
                      <div key={`${idx}-${node.id}-${nIdx}`} className="contents">
                        <span
                          className="inline-flex items-center px-2.5 py-1 rounded-md text-sm font-medium bg-gray-100 text-gray-800"
                        >
                          {node.name}
                          <span className="ml-1 text-xs text-gray-400">({node.type})</span>
                        </span>
                        {nIdx < path.nodes.length - 1 && path.edges[nIdx] && (
                          <span className="text-xs text-blue-600 font-medium bg-blue-50 px-2 py-0.5 rounded">
                            {path.edges[nIdx].type}
                          </span>
                        )}
                      </div>
                    ))}
                  </div>
                </Card>
              ))}
            </div>
          )}
        </>
      )}

      {/* Entity Modal */}
      <Dialog
        open={entityModalOpen}
        onClose={() => {
          setEntityModalOpen(false)
          setFormError('')
        }}
        title="新建实体"
        footer={
          <>
            <Button variant="secondary" size="sm" onClick={() => setEntityModalOpen(false)}>取消</Button>
            <Button size="sm" isLoading={formSubmitting} onClick={handleCreateEntity}>创建</Button>
          </>
        }
      >
        <div className="space-y-4">
          <Input
            label="名称"
            value={entityForm.name}
            onChange={(e) => setEntityForm((s) => ({ ...s, name: e.target.value }))}
          />
          <Input
            label="类型"
            value={entityForm.type}
            onChange={(e) => setEntityForm((s) => ({ ...s, type: e.target.value }))}
          />
          <Textarea
            label="属性（JSON，可选）"
            placeholder={`{ "key": "value" }`}
            rows={4}
            value={entityForm.properties}
            onChange={(e) => setEntityForm((s) => ({ ...s, properties: e.target.value }))}
          />
          {formError && <div className="text-sm text-red-600">{formError}</div>}
        </div>
      </Dialog>

      {/* Relation Modal */}
      <Dialog
        open={relationModalOpen}
        onClose={() => {
          setRelationModalOpen(false)
          setFormError('')
        }}
        title="新建关系"
        footer={
          <>
            <Button variant="secondary" size="sm" onClick={() => setRelationModalOpen(false)}>取消</Button>
            <Button size="sm" isLoading={formSubmitting} onClick={handleCreateRelation}>创建</Button>
          </>
        }
      >
        <div className="space-y-4">
          <Input
            label="源实体"
            value={relationForm.source}
            onChange={(e) => setRelationForm((s) => ({ ...s, source: e.target.value }))}
          />
          <Input
            label="目标实体"
            value={relationForm.target}
            onChange={(e) => setRelationForm((s) => ({ ...s, target: e.target.value }))}
          />
          <Input
            label="关系类型"
            value={relationForm.type}
            onChange={(e) => setRelationForm((s) => ({ ...s, type: e.target.value }))}
          />
          <Textarea
            label="属性（JSON，可选）"
            placeholder={`{ "weight": 1.0 }`}
            rows={4}
            value={relationForm.properties}
            onChange={(e) => setRelationForm((s) => ({ ...s, properties: e.target.value }))}
          />
          {formError && <div className="text-sm text-red-600">{formError}</div>}
        </div>
      </Dialog>
    </div>
  )
}
