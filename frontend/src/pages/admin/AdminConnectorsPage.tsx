import { useEffect, useState, useCallback } from 'react'
import { PageHeader } from '@/components/shared/PageHeader'
import { Card, CardContent } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { Input } from '@/components/ui/Input'
import { Dialog } from '@/components/ui/Dialog'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { connectorsApi } from '@/services/api'
import type { Connector, ConnectorFormData, ConnectorState } from '@/types'
import {
  Plus,
  Trash2,
  Edit2,
  Activity,
  CheckCircle,
  XCircle,
  Clock,
  Database,
  Globe,
  Link2,
  Zap,
  AlertTriangle,
  Loader2,
  Eye,
} from 'lucide-react'

const connectorTypeOptions: Array<{ value: ConnectorFormData['type']; label: string }> = [
  { value: 'http', label: 'HTTP' },
  { value: 'npgsql', label: 'Npgsql / PostgreSQL' },
  { value: 'redis', label: 'Redis' },
  { value: 'messageQueue', label: 'Message Queue / Kafka' },
  { value: 'custom', label: 'Custom' },
]

const databaseLikeTypes: ConnectorFormData['type'][] = ['npgsql', 'redis', 'messageQueue', 'custom']

function isHttpConnector(type: ConnectorFormData['type']) {
  return type === 'http'
}

function needsConnectionString(type: ConnectorFormData['type']) {
  return databaseLikeTypes.includes(type)
}

function getConnectorTarget(connector: Connector): string {
  if (connector.type === 'http') {
    return connector.baseUrl || '未配置 Base URL'
  }

  return connector.connectionString || '未配置连接字符串'
}

function getConnectorKindLabel(type: Connector['type']): string {
  switch (type) {
    case 'http':
      return 'HTTP'
    case 'npgsql':
      return 'PostgreSQL'
    case 'redis':
      return 'Redis'
    case 'messageQueue':
      return 'Kafka / MQ'
    case 'custom':
      return 'Custom'
    default:
      return type
  }
}

const stateConfig: Record<ConnectorState, { label: string; icon: typeof CheckCircle; color: string; badge: string }> = {
  connected: { label: '已连接', icon: Link2, color: 'text-emerald-600', badge: 'success' },
  healthy: { label: '健康', icon: Zap, color: 'text-emerald-600', badge: 'success' },
  connecting: { label: '连接中', icon: Loader2, color: 'text-amber-600', badge: 'warning' },
  disconnected: { label: '未连接', icon: AlertTriangle, color: 'text-gray-500', badge: 'inactive' },
  unhealthy: { label: '异常', icon: AlertTriangle, color: 'text-rose-600', badge: 'error' },
  closed: { label: '已关闭', icon: XCircle, color: 'text-gray-400', badge: 'inactive' },
}

export function AdminConnectorsPage() {
  const [connectors, setConnectors] = useState<Connector[]>([])
  const [loading, setLoading] = useState(false)
  const [dialogOpen, setDialogOpen] = useState(false)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [testingId, setTestingId] = useState<string | null>(null)
  const [detailOpen, setDetailOpen] = useState(false)
  const [detailConnector, setDetailConnector] = useState<Connector | null>(null)
  const [detailHealth, setDetailHealth] = useState<{ isHealthy: boolean; message?: string; responseTimeMs?: number } | null>(null)

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
    if (isHttpConnector(form.type)) {
      if (!form.baseUrl?.trim()) errs.baseUrl = '请输入 Base URL'
      else if (!/^https?:\/\/.+/.test(form.baseUrl)) errs.baseUrl = 'URL 格式不正确'
    }
    if (needsConnectionString(form.type)) {
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

  const openDetail = async (c: Connector) => {
    setDetailConnector(c)
    setDetailHealth(null)
    setDetailOpen(true)
    try {
      const health = await connectorsApi.health(c.id)
      setDetailHealth(health)
    } catch {
      // ignore
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
            {connectors.map(c => {
              const state = stateConfig[c.state] || stateConfig.disconnected
              const StateIcon = state.icon
              return (
                <div key={c.id} className="flex items-center gap-4 px-4 py-3 rounded-lg border border-gray-100 hover:bg-gray-50 transition-colors">
                  <div className="flex-shrink-0">
                    {c.type === 'http' ? (
                      <Globe className={`w-5 h-5 ${state.color}`} />
                    ) : (
                      <Database className={`w-5 h-5 ${state.color}`} />
                    )}
                  </div>
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2 flex-wrap">
                      <span className="text-sm font-medium text-gray-900">{c.name}</span>
                      <span className="text-xs px-2 py-0.5 rounded-full bg-gray-100 text-gray-600 uppercase">{getConnectorKindLabel(c.type)}</span>
                      <StatusBadge status={state.badge}>
                        <span className="flex items-center gap-1">
                          <StateIcon className="w-3 h-3" />
                          {state.label}
                        </span>
                      </StatusBadge>
                    </div>
                    <div className="flex items-center gap-3 mt-1 text-xs text-gray-400 flex-wrap">
                      <span className="truncate max-w-[300px]">
                        {getConnectorTarget(c)}
                      </span>
                      {c.lastHealthCheck && (
                        <span className="flex items-center gap-1">
                          <Clock className="w-3 h-3" />
                          检测于 {new Date(c.lastHealthCheck).toLocaleString('zh-CN')}
                        </span>
                      )}
                    </div>
                  </div>
                  <div className="flex items-center gap-1 flex-shrink-0">
                    <Button variant="ghost" size="sm" onClick={() => openDetail(c)} title="查看详情">
                      <Eye className="w-3.5 h-3.5" />
                    </Button>
                    <Button variant="ghost" size="sm" onClick={() => testHealth(c.id)} isLoading={testingId === c.id} title="健康检查">
                      <Activity className="w-3.5 h-3.5" />
                    </Button>
                    <Button variant="ghost" size="sm" onClick={() => openEdit(c)} title="编辑">
                      <Edit2 className="w-3.5 h-3.5" />
                    </Button>
                    <Button variant="ghost" size="sm" onClick={() => deleteConnector(c.id)} title="删除">
                      <Trash2 className="w-3.5 h-3.5 text-red-500" />
                    </Button>
                  </div>
                </div>
              )
            })}
          </div>
        </CardContent>
      </Card>

      {/* Detail Dialog */}
      <Dialog
        open={detailOpen}
        onClose={() => { setDetailOpen(false); setDetailConnector(null); setDetailHealth(null) }}
        title="连接器详情"
        footer={(
          <>
            <Button variant="ghost" onClick={() => { setDetailOpen(false); setDetailConnector(null); setDetailHealth(null) }}>关闭</Button>
            {detailConnector && (
              <Button onClick={() => { setDetailOpen(false); openEdit(detailConnector) }}>
                <Edit2 className="w-4 h-4 mr-1" /> 编辑
              </Button>
            )}
          </>
        )}
      >
        {detailConnector && (
          <div className="space-y-4">
            <div className="flex items-center gap-3">
              {detailConnector.type === 'http' ? (
                <Globe className="w-8 h-8 text-blue-500" />
              ) : (
                <Database className="w-8 h-8 text-emerald-500" />
              )}
              <div>
                <p className="text-base font-semibold text-gray-900">{detailConnector.name}</p>
                <p className="text-xs text-gray-400 uppercase">{getConnectorKindLabel(detailConnector.type)}</p>
              </div>
            </div>

            <div className="grid grid-cols-2 gap-3">
              <div className="bg-gray-50 rounded-lg p-3">
                <p className="text-xs text-gray-400 mb-1">连接状态</p>
                {(() => {
                  const s = stateConfig[detailConnector.state] || stateConfig.disconnected
                  const Icon = s.icon
                  return (
                    <span className="inline-flex items-center gap-1.5 text-sm font-medium">
                      <Icon className={`w-4 h-4 ${s.color}`} />
                      {s.label}
                    </span>
                  )
                })()}
              </div>
              <div className="bg-gray-50 rounded-lg p-3">
                <p className="text-xs text-gray-400 mb-1">健康状态</p>
                <span className="inline-flex items-center gap-1.5 text-sm font-medium">
                  {detailConnector.state === 'healthy' || detailConnector.state === 'connected' ? (
                    <>
                      <CheckCircle className="w-4 h-4 text-emerald-600" />
                      <span className="text-emerald-700">健康</span>
                    </>
                  ) : (
                    <>
                      <XCircle className="w-4 h-4 text-rose-600" />
                      <span className="text-rose-700">异常</span>
                    </>
                  )}
                </span>
              </div>
            </div>

            {detailHealth && (
              <div className="bg-gray-50 rounded-lg p-3 space-y-2">
                <p className="text-xs font-medium text-gray-700">最近一次检测</p>
                <div className="flex items-center gap-4 text-sm">
                  <span className="flex items-center gap-1">
                    <Activity className="w-3.5 h-3.5 text-gray-400" />
                    {detailHealth.isHealthy ? '通过' : '未通过'}
                  </span>
                  {typeof detailHealth.responseTimeMs === 'number' && (
                    <span className="flex items-center gap-1">
                      <Clock className="w-3.5 h-3.5 text-gray-400" />
                      {detailHealth.responseTimeMs.toFixed(1)} ms
                    </span>
                  )}
                </div>
                {detailHealth.message && (
                  <p className="text-xs text-gray-500">{detailHealth.message}</p>
                )}
              </div>
            )}

            <div className="space-y-2">
              <p className="text-xs font-medium text-gray-700">配置信息</p>
              {detailConnector.type === 'http' ? (
                <>
                  <div className="bg-gray-50 rounded-lg p-3">
                    <p className="text-xs text-gray-400 mb-1">Base URL</p>
                    <p className="text-sm text-gray-900 font-mono break-all">{detailConnector.baseUrl || '-'}</p>
                  </div>
                  {detailConnector.endpoints && detailConnector.endpoints.length > 0 && (
                    <div className="bg-gray-50 rounded-lg p-3">
                      <p className="text-xs text-gray-400 mb-2">端点列表</p>
                      <div className="space-y-1.5">
                        {detailConnector.endpoints.map((ep, i) => (
                          <div key={i} className="flex items-center gap-2 text-xs">
                            <span className="px-1.5 py-0.5 rounded bg-blue-100 text-blue-700 font-medium">{ep.method}</span>
                            <span className="text-gray-500 font-medium min-w-[60px]">{ep.name}</span>
                            <span className="text-gray-400 truncate">{ep.url}</span>
                          </div>
                        ))}
                      </div>
                    </div>
                  )}
                </>
              ) : (
                <>
                  <div className="bg-gray-50 rounded-lg p-3">
                    <p className="text-xs text-gray-400 mb-1">连接字符串</p>
                    <p className="text-sm text-gray-900 font-mono break-all">{detailConnector.connectionString || '-'}</p>
                  </div>
                  <div className="bg-gray-50 rounded-lg p-3">
                    <p className="text-xs text-gray-400 mb-1">Command Timeout</p>
                    <p className="text-sm text-gray-900">{detailConnector.commandTimeout || 30} 秒</p>
                  </div>
                </>
              )}
            </div>

            {detailConnector.lastHealthCheck && (
              <p className="text-xs text-gray-400 text-right">
                上次检测: {new Date(detailConnector.lastHealthCheck).toLocaleString('zh-CN')}
              </p>
            )}
          </div>
        )}
      </Dialog>

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
              onChange={e => setForm(prev => ({ ...prev, type: e.target.value as ConnectorFormData['type'] }))}
            >
              {connectorTypeOptions.map((option) => (
                <option key={option.value} value={option.value}>{option.label}</option>
              ))}
            </select>
          </div>
          {isHttpConnector(form.type) && (
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
          {needsConnectionString(form.type) && (
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
