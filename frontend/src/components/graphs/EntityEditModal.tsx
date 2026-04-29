import { useState, useEffect } from 'react'
import { Button } from '@/components/ui/Button'
import { Dialog } from '@/components/ui/Dialog'
import { Input } from '@/components/ui/Input'
import { Select } from '@/components/ui/Select'
import { graphApi } from '@/services/api'
import type { GraphEntity } from '@/types'

interface EntityEditModalProps {
  open: boolean
  entity?: GraphEntity | null
  onClose: () => void
  onSuccess: () => void
}

const entityTypeOptions = [
  { value: '人物', label: '人物' },
  { value: '组织', label: '组织' },
  { value: '产品', label: '产品' },
  { value: '技术', label: '技术' },
  { value: '项目', label: '项目' },
  { value: '文档', label: '文档' },
  { value: '其他', label: '其他' },
]

interface PropertyItem {
  key: string
  value: string
}

export function EntityEditModal({ open, entity, onClose, onSuccess }: EntityEditModalProps) {
  const isEdit = !!entity
  const [name, setName] = useState('')
  const [type, setType] = useState('')
  const [properties, setProperties] = useState<PropertyItem[]>([])
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [submitting, setSubmitting] = useState(false)
  const [apiError, setApiError] = useState('')

  useEffect(() => {
    if (open) {
      if (entity) {
        setName(entity.name)
        setType(entity.type)
        const props: PropertyItem[] = []
        if (entity.properties) {
          Object.entries(entity.properties).forEach(([k, v]) => {
            props.push({ key: k, value: typeof v === 'string' ? v : JSON.stringify(v) })
          })
        }
        setProperties(props.length > 0 ? props : [{ key: '', value: '' }])
      } else {
        setName('')
        setType('')
        setProperties([{ key: '', value: '' }])
      }
      setErrors({})
      setApiError('')
      setSubmitting(false)
    }
  }, [open, entity])

  const validate = (): boolean => {
    const newErrors: Record<string, string> = {}
    if (!name.trim()) newErrors.name = '名称不能为空'
    if (!type.trim()) newErrors.type = '类型不能为空'
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
      if (isEdit && entity) {
        await graphApi.updateEntity(entity.id, {
          name: name.trim(),
          type: type.trim(),
          properties: props,
          departmentId: entity.departmentId ?? null,
          isPublic: entity.isPublic ?? false,
        })
      } else {
        await graphApi.createEntity({
          name: name.trim(),
          type: type.trim(),
          properties: props,
          departmentId: null,
          isPublic: false,
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

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={isEdit ? '编辑实体' : '新建实体'}
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
        <Input
          label="实体名称"
          value={name}
          onChange={(e) => setName(e.target.value)}
          error={errors.name}
          placeholder="请输入实体名称"
        />
        <Select
          label="实体类型"
          value={type}
          onChange={(e) => setType(e.target.value)}
          error={errors.type}
          options={entityTypeOptions}
          placeholder="请选择类型"
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
