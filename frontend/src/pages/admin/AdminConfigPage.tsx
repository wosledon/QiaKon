import { useEffect, useState, useCallback } from 'react'
import { PageHeader } from '@/components/shared/PageHeader'
import { Card, CardHeader, CardContent } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { Input } from '@/components/ui/Input'
import { Textarea } from '@/components/ui/Textarea'
import { configApi } from '@/services/api'
import type { SystemConfig } from '@/types'
import { Save, RotateCcw } from 'lucide-react'

export function AdminConfigPage() {
  const [config, setConfig] = useState<SystemConfig | null>(null)
  const [loading, setLoading] = useState(false)
  const [saving, setSaving] = useState(false)

  const fetchConfig = useCallback(async () => {
    setLoading(true)
    try {
      const data = await configApi.get()
      setConfig(data)
    } catch (e: unknown) {
      alert(e instanceof Error ? e.message : '加载配置失败')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    fetchConfig()
  }, [fetchConfig])

  const handleSave = async () => {
    if (!config) return
    setSaving(true)
    try {
      await configApi.update(config)
      alert('保存成功')
    } catch (e: unknown) {
      alert(e instanceof Error ? e.message : '保存失败')
    } finally {
      setSaving(false)
    }
  }

  const handleReset = async () => {
    if (!window.confirm('确定重置为默认配置？当前修改将丢失。')) return
    setSaving(true)
    try {
      const data = await configApi.reset()
      setConfig(data)
      alert('已重置为默认配置')
    } catch (e: unknown) {
      alert(e instanceof Error ? e.message : '重置失败')
    } finally {
      setSaving(false)
    }
  }

  const updateField = <K extends keyof SystemConfig>(key: K, value: SystemConfig[K]) => {
    setConfig(prev => prev ? { ...prev, [key]: value } : prev)
  }

  if (!config && loading) {
    return (
      <div>
        <PageHeader title="系统配置" description="平台全局参数与运行时配置" />
        <div className="text-center py-12 text-gray-400">加载中...</div>
      </div>
    )
  }

  return (
    <div>
      <PageHeader title="系统配置" description="平台全局参数与运行时配置">
        <div className="flex gap-2">
          <Button variant="secondary" size="sm" onClick={handleReset} isLoading={saving}>
            <RotateCcw className="w-4 h-4 mr-1" /> 重置
          </Button>
          <Button size="sm" onClick={handleSave} isLoading={saving}>
            <Save className="w-4 h-4 mr-1" /> 保存
          </Button>
        </div>
      </PageHeader>

      <div className="space-y-6">
        <Card>
          <CardHeader>
            <h3 className="text-base font-semibold text-gray-900">分块策略配置</h3>
          </CardHeader>
          <CardContent className="space-y-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">默认分块策略</label>
              <select
                className="w-full px-4 py-2 rounded-lg border border-gray-300 bg-white text-gray-900 focus:outline-none focus:ring-2 focus:ring-blue-500"
                value={config?.chunkingStrategy || 'fixed'}
                onChange={e => updateField('chunkingStrategy', e.target.value)}
              >
                <option value="fixed">固定长度分块</option>
                <option value="semantic">语义分块</option>
                <option value="recursive">递归分块</option>
                <option value="moe">MoE 智能分块</option>
              </select>
            </div>
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <Input
                label="分块大小"
                type="number"
                value={config?.chunkSize ?? 512}
                onChange={e => updateField('chunkSize', Number(e.target.value))}
              />
              <Input
                label="重叠度"
                type="number"
                value={config?.chunkOverlap ?? 0}
                onChange={e => updateField('chunkOverlap', Number(e.target.value))}
              />
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <h3 className="text-base font-semibold text-gray-900">向量维度配置</h3>
          </CardHeader>
          <CardContent>
            <Input
              label="Embedding 向量维度"
              type="number"
              value={config?.embeddingDimension ?? 1536}
              onChange={e => updateField('embeddingDimension', Number(e.target.value))}
            />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <h3 className="text-base font-semibold text-gray-900">缓存策略配置</h3>
          </CardHeader>
          <CardContent className="space-y-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">缓存层级</label>
              <div className="flex gap-2">
                {['memory', 'redis', 'disk'].map(level => (
                  <label key={level} className="flex items-center gap-1.5 text-sm text-gray-700 px-3 py-2 rounded-lg border border-gray-200">
                    <input
                      type="checkbox"
                      checked={config?.cacheLevels?.includes(level) ?? false}
                      onChange={e => {
                        const levels = new Set(config?.cacheLevels || [])
                        if (e.target.checked) levels.add(level)
                        else levels.delete(level)
                        updateField('cacheLevels', Array.from(levels))
                      }}
                      className="rounded border-gray-300 text-blue-600 focus:ring-blue-500"
                    />
                    {level === 'memory' && '内存缓存'}
                    {level === 'redis' && 'Redis缓存'}
                    {level === 'disk' && '磁盘缓存'}
                  </label>
                ))}
              </div>
            </div>
            <Input
              label="缓存过期时间（秒）"
              type="number"
              value={config?.cacheTtlSeconds ?? 3600}
              onChange={e => updateField('cacheTtlSeconds', Number(e.target.value))}
            />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <h3 className="text-base font-semibold text-gray-900">Prompt 模板管理</h3>
          </CardHeader>
          <CardContent>
            <Textarea
              label="系统级 Prompt 模板"
              rows={8}
              value={config?.systemPromptTemplate || ''}
              onChange={e => updateField('systemPromptTemplate', e.target.value)}
              placeholder="输入系统级 Prompt 模板..."
            />
          </CardContent>
        </Card>
      </div>
    </div>
  )
}
