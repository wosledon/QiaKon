import { useState, useEffect, useCallback } from 'react'
import { Button } from '@/components/ui/Button'
import { Dialog } from '@/components/ui/Dialog'
import { Input } from '@/components/ui/Input'
import { Select } from '@/components/ui/Select'
import { graphApi } from '@/services/api'
import type { GraphEntity, GraphRelation } from '@/types'

interface RelationEditModalProps {
  open: boolean
  relation?: GraphRelation | null
  onClose: () => void
  onSuccess: () => void
}

const relationTypeOptions = [
  { value: '属于', label: '属于' },
  { value: '负责', label: '负责' },
  { value: '包含', label: '包含' },
  { value: '依赖', label: '依赖' },
  { value: '关联', label: '关联' },
  { value: '引用', label: '引用' },
  { value: '其他', label: '其他' },
]

interface PropertyItem {
  key: string
  value: string
}

export function RelationEditModal({ open, relation, onClose, onSuccess }: RelationEditModalProps) {
  const isEdit = !!relation
  const [sourceQuery, setSourceQuery] = useState('')
  const [targetQuery, setTargetQuery] = useState('')
  const [sourceResults, setSourceResults] = useState<GraphEntity[]>([])
  const [targetResults, setTargetResults] = useState<GraphEntity[]>([])
  const [sourceId, setSourceId] = useState('')
  const [sourceName, setSourceName] = useState('')
  const [targetId, setTargetId] = useState('')
  const [targetName, setTargetName] = useState('')
  const [type, setType] = useState('')
  const [properties, setProperties] = useState<PropertyItem[]>([])
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [submitting, setSubmitting] = useState(false)
  const [apiError, setApiError] = useState('')
  const [showSourceDropdown, setShowSourceDropdown] = useState(false)
  const [showTargetDropdown, setShowTargetDropdown] = useState(false)

  useEffect(() => {
    if (open) {
      if (relation) {
        setSourceId(relation.sourceId)
        setSourceName(relation.sourceName)
        setTargetId(relation.targetId)
        setTargetName(relation.targetName)
        setType(relation.type)
        const props: PropertyItem[] = []
        if (relation.properties) {
          Object.entries(relation.properties).forEach(([k, v]) => {
            props.push({ key: k, value: typeof v === 'string' ? v : JSON.stringify(v) })
          })
        }
        setProperties(props.length > 0 ? props : [])
        setSourceQuery(relation.sourceName)
        setTargetQuery(relation.targetName)
      } else {
        setSourceId('')
        setSourceName('')
        setTargetId('')
        setTargetName('')
        setType('')
        setProperties([])
        setSourceQuery('')
        setTargetQuery('')
      }
      setErrors({})
      setApiError('')
      setSubmitting(false)
      setShowSourceDropdown(false)
      setShowTargetDropdown(false)
    }
  }, [open, relation])

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

  const handleSourceSearch = useCallback((value: string) => {
    setSourceQuery(value)
    setShowSourceDropdown(true)
    searchEntities(value, setSourceResults)
  }, [searchEntities])

  const handleTargetSearch = useCallback((value: string) => {
    setTargetQuery(value)
    setShowTargetDropdown(true)
    searchEntities(value, setTargetResults)
  }, [searchEntities])

  const handleSelectSource = (entity: GraphEntity) => {
    setSourceId(entity.id)
    setSourceName(entity.name)
    setSourceQuery(entity.name)
    setShowSourceDropdown(false)
  }

  const handleSelectTarget = (entity: GraphEntity) => {
    setTargetId(entity.id)
    setTargetName(entity.name)
    setTargetQuery(entity.name)
    setShowTargetDropdown(false)
  }

  const validate = (): boolean => {
    const newErrors: Record<string, string> = {}
    if (!sourceId) newErrors.source = '源实体不能为空'
    if (!targetId) newErrors.target = '目标实体不能为空'
    if (!type.trim()) newErrors.type = '关系类型不能为空'
    setErrors(newErrors)
    return Object.keys(newErrors).length === 0
  }

  const handleAddProperty = () => {
    setProperties((prev) => [...prev, { key: '', value: '' }])
  }

  const handleRemoveProperty = (index: number) => {
    setProperties((prev) => prev.filter((_, i) => i !== index))
  }

  const handlePropertyChange = (index: number, field: 'key' | 'value', val: string) => {
    setProperties((prev) =>
      prev.map((p, i) => (i === index ? { ...p, [field]: val } : p))
    )
  }

  const handleSubmit = async () => {
    if (!validate()) return
    setApiError('')
    setSubmitting(true)

    const props: Record<string, unknown> = {}
    properties.forEach((p) => {
      if (p.key.trim()) {
        try {
          props[p.key.trim()] = JSON.parse(p.value)
        } catch {
          props[p.key.trim()] = p.value
        }
      }
    })

    try {
      if (isEdit && relation) {
        await graphApi.updateRelation(relation.id, {
          sourceId,
          targetId,
          type: type.trim(),
          properties: props,
        })
      } else {
        await graphApi.createRelation({
          sourceId,
          targetId,
          type: type.trim(),
          properties: props,
        })
      }
      onSuccess()
      onClose()
    } catch (err) {
      setApiError(err instanceof Error ? err.message : '提交失败')
    } finally {
      setSubmitting(false)
    }
  }

  const renderEntitySearch = (
    label: string,
    query: string,
    onQueryChange: (v: string) => void,
    results: GraphEntity[],
    showDropdown: boolean,
    selectedName: string,
    error?: string
  ) => (
    <div className="relative">
      <label className="block text-sm font-medium text-gray-700 mb-1">{label}</label>
      <Input
        value={query}
        onChange={(e) => onQueryChange(e.target.value)}
        onFocus={() => onQueryChange(query)}
        placeholder="搜索实体名称"
        error={error}
      />
      {showDropdown && results.length > 0 && (
        <div className="absolute z-10 mt-1 w-full bg-white border border-gray-200 rounded-lg shadow-lg max-h-48 overflow-y-auto">
          {results.map((entity) => (
            <button
              key={entity.id}
              className="w-full text-left px-4 py-2 hover:bg-gray-50 text-sm"
              onClick={() =>
                label.includes('源')
                  ? handleSelectSource(entity)
                  : handleSelectTarget(entity)
              }
            >
              <span className="font-medium text-gray-900">{entity.name}</span>
              <span className="ml-2 text-xs text-gray-500">{entity.type}</span>
            </button>
          ))}
        </div>
      )}
      {selectedName && !showDropdown && (
        <p className="mt-1 text-xs text-gray-500">已选择: {selectedName}</p>
      )}
    </div>
  )

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={isEdit ? '编辑关系' : '新建关系'}
      footer={
        <>
          <Button variant="secondary" size="sm" onClick={onClose} disabled={submitting}>
            取消
          </Button>
          <Button size="sm" onClick={handleSubmit} isLoading={submitting}>
            {isEdit ? '保存' : '创建'}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        {apiError && (
          <div className="rounded-lg bg-red-50 px-4 py-3 text-sm text-red-600">{apiError}</div>
        )}
        {renderEntitySearch(
          '源实体',
          sourceQuery,
          handleSourceSearch,
          sourceResults,
          showSourceDropdown,
          sourceName,
          errors.source
        )}
        {renderEntitySearch(
          '目标实体',
          targetQuery,
          handleTargetSearch,
          targetResults,
          showTargetDropdown,
          targetName,
          errors.target
        )}
        <Select
          label="关系类型"
          value={type}
          onChange={(e) => setType(e.target.value)}
          error={errors.type}
          options={relationTypeOptions}
          placeholder="请选择关系类型"
        />
        <div>
          <div className="flex items-center justify-between mb-2">
            <label className="text-sm font-medium text-gray-700">属性</label>
            <Button variant="ghost" size="sm" onClick={handleAddProperty}>
              + 添加属性
            </Button>
          </div>
          <div className="space-y-2 max-h-48 overflow-y-auto">
            {properties.map((prop, index) => (
              <div key={index} className="flex items-center gap-2">
                <Input
                  placeholder="键"
                  value={prop.key}
                  onChange={(e) => handlePropertyChange(index, 'key', e.target.value)}
                  className="flex-1"
                />
                <Input
                  placeholder="值"
                  value={prop.value}
                  onChange={(e) => handlePropertyChange(index, 'value', e.target.value)}
                  className="flex-1"
                />
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={() => handleRemoveProperty(index)}
                  className="text-gray-400 hover:text-red-600 flex-shrink-0"
                >
                  删除
                </Button>
              </div>
            ))}
            {properties.length === 0 && (
              <p className="text-sm text-gray-400">暂无属性，点击上方按钮添加</p>
            )}
          </div>
        </div>
      </div>
    </Dialog>
  )
}
