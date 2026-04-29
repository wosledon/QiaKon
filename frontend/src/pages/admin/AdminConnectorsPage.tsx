import { useEffect, useState, useCallback } from 'react'
import { PageHeader } from '@/components/shared/PageHeader'
import { Card, CardContent } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { Input } from '@/components/ui/Input'
import { Dialog } from '@/components/ui/Dialog'
import { connectorsApi } from '@/services/api'
import type { Connector, ConnectorFormData } from '@/types'
import { Plus, Trash2, Edit2, Activity, CheckCircle, XCircle } from 'lucide-react'

export function AdminConnectorsPage() {
  const [connectors, setConnectors] = useState<Connector[]>([])
  const [loading, setLoading] = useState(false)
  const [dialogOpen, setDialogOpen] = useState(false)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [testingId, setTestingId] = useState<string | null>(null)

  const [form, setForm] = useState<ConnectorFormData>({
    name: '', type: 'http', baseUrl: '', endpoints: [{ name: '', url: '', method: 'GET' }], connectionString: '', commandTimeout: 30,
  })
  const [errors, setErrors] = useState<Partial<Record<keyof ConnectorFormData, string>>>({})

  const fetchData = useCallback(async () => {
    setLoading(true)
    try {
      const data = await connectorsApi.list()
      setConnectors(data)
    } catch {
      // ignore
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    fetchData()
  }, [fetchData])

  const validate = (): boolean => {
    const errs: Partial<Record<keyof ConnectorFormData, string>> = {}
    if (!form.name.trim()) errs.name = '请输入连接器名称'
    if (form.type === 'http') {
      if (!form.baseUrl?.trim()) errs.baseUrl = '请输入 Base URL'
      else if (!/^https?:\/\/.+/.test(form.baseUrl)) errs.baseUrl = 'URL 格式不正确'
    }
    if (form.type === 'npgsql') {
      if (!form.connectionString?.trim()) errs.connectionString = '请输入连接字符串'
    }
    setErrors(errs)
    return Object.keys(errs).length === 0
  }

  const save = async () => {
    if (!validate()) return
    try {
      if (editingId) {
        await connectorsApi.update(editingId, form)
      } else {
        await connectorsApi.add(form)
      }
      setDialogOpen(false)
      resetForm()
      fetchData()
    } catch (e: unknown) {
      alert(e instanceof Error ? e.message : '保存失败')
    }
  }

  const resetForm = () => {
    setForm({ name: '', type: 'http', baseUrl: '', endpoints: [{ name: '', url: '', method: 'GET' }], connectionString: '', commandTimeout: 30 })
    setErrors({})
    setEditingId(null)
  }

  const openAdd = () => {
    resetForm()
    setDialogOpen(true)
  }

  const openEdit = (c: Connector) => {
    setForm({
      name: c.name,
      type: c.type,
      baseUrl: c.baseUrl || '',
      endpoints: c.endpoints?.length ? c.endpoints : [{ name: '', url: '', method: 'GET' }],
      connectionString: c.connectionString || '',
      commandTimeout: c.commandTimeout || 30,
    })
    setEditingId(c.id)
    setDialogOpen(true)
  }

  const deleteConnector = async (id: string) => {
    if (!window.confirm('确定删除该连接器？')) return
    try {
      await connectorsApi.delete(id)
      fetchData()
    } catch (e: unknown) {
      alert(e instanceof Error ? e.message : '删除失败')
    }
  }

  const testHealth = async (id: string) => {
    setTestingId(id)
    try {
      const res = await connectorsApi.health(id)
      alert(res.isHealthy ? '健康检查通过' : `健康检查未通过: ${res.message || ''}`)
      fetchData()
    } catch (e: unknown) {
      alert(e instanceof Error ? e.message : '检测失败')
    } finally {
      setTestingId(null)
    }
  }

  const updateEndpoint = (index: number, field: string, value: string) => {
    const eps = [...(form.endpoints || [])]
    eps[index] = { ...eps[index], [field]: value }
    setForm(prev => ({ ...prev, endpoints: eps }))
  }

  const addEndpoint = () => {
    setForm(prev => ({ ...prev, endpoints: [...(prev.endpoints || []), { name: '', url: '', method: 'GET' }] }))
  }

  const removeEndpoint = (index: number) => {
    setForm(prev => ({ ...prev, endpoints: (prev.endpoints || []).filter((_, i) => i !== index) }))
  }

  return (
    <div className="mx-auto w-full max-w-7xl p-4 md:p-8">
      <PageHeader title="连接器管理" description="配置外部系统连接器">
        <Button onClick={openAdd} size="sm">
          <Plus className="w-4 h-4 mr-1" /> 添加连接器
        </Button>
      </PageHeader>

      <Card>
        <CardContent className="py-4">
          {connectors.length === 0 && !loading && (
            <p className="text-sm text-gray-400 text-center py-8">暂无连接器配置</p>
          )}
          <div className="space-y-3">
            {connectors.map(c => (
              <div key={c.id} className="flex items-center gap-4 px-4 py-3 rounded-lg border border-gray-100 hover:bg-gray-50">
                <div className="flex-shrink-0">
                  {c.isHealthy ? (
                    <CheckCircle className="w-5 h-5 text-emerald-500" />
                  ) : (
                    <XCircle className="w-5 h-5 text-rose-500" />
                  )}
                </div>
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2">
                    <span className="text-sm font-medium text-gray-900">{c.name}</span>
                    <span className="text-xs px-2 py-0.5 rounded-full bg-gray-100 text-gray-600 uppercase">{c.type}</span>
                  </div>
                  <p className="text-xs text-gray-400 truncate">
                    {c.type === 'http' ? c.baseUrl : c.connectionString}
                  </p>
                </div>
                <div className="flex items-center gap-1 flex-shrink-0">
                  <Button variant="ghost" size="sm" onClick={() => testHealth(c.id)} isLoading={testingId === c.id}>
                    <Activity className="w-3.5 h-3.5" />
                  </Button>
                  <Button variant="ghost" size="sm" onClick={() => openEdit(c)}>
                    <Edit2 className="w-3.5 h-3.5" />
                  </Button>
                  <Button variant="ghost" size="sm" onClick={() => deleteConnector(c.id)}>
                    <Trash2 className="w-3.5 h-3.5 text-red-500" />
                  </Button>
                </div>
              </div>
            ))}
          </div>
        </CardContent>
      </Card>

      <Dialog
        open={dialogOpen}
        onClose={() => { setDialogOpen(false); resetForm() }}
        title={editingId ? '编辑连接器' : '添加连接器'}
        footer={(
          <>
            <Button variant="ghost" onClick={() => { setDialogOpen(false); resetForm() }}>取消</Button>
            <Button onClick={save}>保存</Button>
          </>
        )}
      >
        <div className="space-y-4">
          <Input label="名称" value={form.name} onChange={e => setForm(prev => ({ ...prev, name: e.target.value }))} error={errors.name} />
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">类型</label>
            <select
              className="w-full px-4 py-2 rounded-lg border border-gray-300 bg-white text-gray-900 focus:outline-none focus:ring-2 focus:ring-blue-500"
              value={form.type}
              onChange={e => setForm(prev => ({ ...prev, type: e.target.value as 'http' | 'npgsql' }))}
            >
              <option value="http">HTTP</option>
              <option value="npgsql">Npgsql</option>
            </select>
          </div>
          {form.type === 'http' && (
            <>
              <Input label="Base URL" value={form.baseUrl || ''} onChange={e => setForm(prev => ({ ...prev, baseUrl: e.target.value }))} error={errors.baseUrl} />
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">端点列表</label>
                <div className="space-y-2">
                  {form.endpoints?.map((ep, i) => (
                    <div key={i} className="flex gap-2 items-start">
                      <input
                        className="w-24 px-2 py-2 rounded-lg border border-gray-300 text-sm"
                        placeholder="名称"
                        value={ep.name}
                        onChange={e => updateEndpoint(i, 'name', e.target.value)}
                      />
                      <select
                        className="w-24 px-2 py-2 rounded-lg border border-gray-300 text-sm"
                        value={ep.method}
                        onChange={e => updateEndpoint(i, 'method', e.target.value)}
                      >
                        <option>GET</option>
                        <option>POST</option>
                        <option>PUT</option>
                        <option>DELETE</option>
                      </select>
                      <input
                        className="flex-1 px-2 py-2 rounded-lg border border-gray-300 text-sm"
                        placeholder="URL"
                        value={ep.url}
                        onChange={e => updateEndpoint(i, 'url', e.target.value)}
                      />
                      <Button variant="ghost" size="sm" onClick={() => removeEndpoint(i)}>
                        <Trash2 className="w-3.5 h-3.5 text-red-500" />
                      </Button>
                    </div>
                  ))}
                </div>
                <Button variant="ghost" size="sm" onClick={addEndpoint} className="mt-2">
                  <Plus className="w-3.5 h-3.5 mr-1" /> 添加端点
                </Button>
              </div>
            </>
          )}
          {form.type === 'npgsql' && (
            <>
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">连接字符串</label>
                <textarea
                  className="w-full px-4 py-2 rounded-lg border border-gray-300 bg-white text-gray-900 focus:outline-none focus:ring-2 focus:ring-blue-500 text-sm"
                  rows={3}
                  value={form.connectionString || ''}
                  onChange={e => setForm(prev => ({ ...prev, connectionString: e.target.value }))}
                />
                {errors.connectionString && <p className="mt-1 text-sm text-red-600">{errors.connectionString}</p>}
              </div>
              <Input label="Command Timeout（秒）" type="number" value={form.commandTimeout || ''} onChange={e => setForm(prev => ({ ...prev, commandTimeout: Number(e.target.value) }))} />
            </>
          )}
        </div>
      </Dialog>
    </div>
  )
}
