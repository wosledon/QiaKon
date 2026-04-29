import { useEffect, useState, useCallback } from 'react'
import { PageHeader } from '@/components/shared/PageHeader'
import { Card, CardHeader, CardContent } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { Input } from '@/components/ui/Input'
import { Dialog } from '@/components/ui/Dialog'
import { llmModelsApi } from '@/services/api'
import type { LlmProvider, LlmModel, EmbeddingModel, ProviderFormData, ModelFormData } from '@/types'
import { ChevronDown, ChevronRight, Plus, Trash2, Edit2, Star, TestTube, Power, PowerOff } from 'lucide-react'

export function AdminLlmModelsPage() {
  const [providers, setProviders] = useState<LlmProvider[]>([])
  const [embeddings, setEmbeddings] = useState<EmbeddingModel[]>([])
  const [expanded, setExpanded] = useState<Set<string>>(new Set())
  const [loading, setLoading] = useState(false)

  const [providerDialog, setProviderDialog] = useState(false)
  const [providerForm, setProviderForm] = useState<ProviderFormData>({
    name: '', type: 'openai', apiKey: '', baseUrl: '', timeout: 60, retryCount: 3,
  })
  const [providerErrors, setProviderErrors] = useState<Partial<Record<keyof ProviderFormData, string>>>({})
  const [editingProvider, setEditingProvider] = useState<string | null>(null)
  const [testingProvider, setTestingProvider] = useState<string | null>(null)

  const [modelDialog, setModelDialog] = useState(false)
  const [modelForm, setModelForm] = useState<ModelFormData & { providerId: string }>({
    providerId: '', type: 'inference', name: '', modelName: '', maxTokens: undefined, dimension: undefined, isDefault: false,
  })
  const [modelErrors, setModelErrors] = useState<Partial<Record<keyof ModelFormData, string>>>({})
  const [editingModel, setEditingModel] = useState<string | null>(null)

  const fetchData = useCallback(async () => {
    setLoading(true)
    try {
      const [p, e] = await Promise.all([
        llmModelsApi.providers(),
        llmModelsApi.embeddings(),
      ])
      setProviders(p)
      setEmbeddings(e)
    } catch {
      // ignore
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    fetchData()
  }, [fetchData])

  const toggleExpand = (id: string) => {
    setExpanded(prev => {
      const next = new Set(prev)
      if (next.has(id)) next.delete(id)
      else next.add(id)
      return next
    })
  }

  const validateProvider = (): boolean => {
    const errors: Partial<Record<keyof ProviderFormData, string>> = {}
    if (!providerForm.name.trim()) errors.name = '请输入供应商名称'
    if (!providerForm.baseUrl.trim()) errors.baseUrl = '请输入 Base URL'
    else if (!/^https?:\/\/.+/.test(providerForm.baseUrl)) errors.baseUrl = 'URL 格式不正确'
    if (!providerForm.apiKey.trim()) errors.apiKey = '请输入 API Key'
    setProviderErrors(errors)
    return Object.keys(errors).length === 0
  }

  const saveProvider = async () => {
    if (!validateProvider()) return
    try {
      if (editingProvider) {
        await llmModelsApi.updateProvider(editingProvider, providerForm)
      } else {
        await llmModelsApi.addProvider(providerForm)
      }
      setProviderDialog(false)
      resetProviderForm()
      fetchData()
    } catch (e: unknown) {
      const msg = e instanceof Error ? e.message : '保存失败'
      alert(msg)
    }
  }

  const resetProviderForm = () => {
    setProviderForm({ name: '', type: 'openai', apiKey: '', baseUrl: '', timeout: 60, retryCount: 3 })
    setProviderErrors({})
    setEditingProvider(null)
  }

  const testConnection = async (id: string) => {
    setTestingProvider(id)
    try {
      const res = await llmModelsApi.testProvider(id)
      alert(res.success ? '连接成功' : `连接失败: ${res.message || ''}`)
    } catch (e: unknown) {
      alert(e instanceof Error ? e.message : '测试失败')
    } finally {
      setTestingProvider(null)
    }
  }

  const deleteProvider = async (id: string) => {
    if (!window.confirm('确定删除该供应商？其下所有模型配置也将被删除。')) return
    try {
      await llmModelsApi.deleteProvider(id)
      fetchData()
    } catch (e: unknown) {
      alert(e instanceof Error ? e.message : '删除失败')
    }
  }

  const openAddProvider = () => {
    resetProviderForm()
    setProviderDialog(true)
  }

  const openEditProvider = (p: LlmProvider) => {
    setProviderForm({
      name: p.name,
      type: p.type,
      apiKey: p.apiKey || '',
      baseUrl: p.baseUrl,
      timeout: p.timeout ?? 60,
      retryCount: p.retryCount ?? 3,
    })
    setEditingProvider(p.id)
    setProviderDialog(true)
  }

  const validateModel = (): boolean => {
    const errors: Partial<Record<keyof ModelFormData, string>> = {}
    if (!modelForm.name.trim()) errors.name = '请输入模型名称'
    if (!modelForm.modelName.trim()) errors.modelName = '请输入实际模型名'
    if (modelForm.type === 'embedding' && (!modelForm.dimension || modelForm.dimension <= 0)) {
      errors.dimension = '分块模型必须填写向量维度'
    }
    setModelErrors(errors)
    return Object.keys(errors).length === 0
  }

  const saveModel = async () => {
    if (!validateModel()) return
    try {
      if (editingModel) {
        await llmModelsApi.updateModel(editingModel, modelForm)
      } else {
        await llmModelsApi.addModel(modelForm)
      }
      setModelDialog(false)
      resetModelForm()
      fetchData()
    } catch (e: unknown) {
      alert(e instanceof Error ? e.message : '保存失败')
    }
  }

  const resetModelForm = () => {
    setModelForm({ providerId: '', type: 'inference', name: '', modelName: '', maxTokens: undefined, dimension: undefined, isDefault: false })
    setModelErrors({})
    setEditingModel(null)
  }

  const openAddModel = (providerId: string) => {
    resetModelForm()
    setModelForm(prev => ({ ...prev, providerId }))
    setModelDialog(true)
  }

  const openEditModel = (m: LlmModel, providerId: string) => {
    setModelForm({
      providerId,
      type: m.type,
      name: m.name,
      modelName: m.modelName,
      maxTokens: m.maxTokens,
      dimension: m.dimension,
      isDefault: m.isDefault,
    })
    setEditingModel(m.id)
    setModelDialog(true)
  }

  const deleteModel = async (id: string) => {
    if (!window.confirm('确定删除该模型？')) return
    try {
      await llmModelsApi.deleteModel(id)
      fetchData()
    } catch (e: unknown) {
      alert(e instanceof Error ? e.message : '删除失败')
    }
  }

  const setDefault = async (id: string) => {
    try {
      await llmModelsApi.setDefaultModel(id)
      fetchData()
    } catch (e: unknown) {
      alert(e instanceof Error ? e.message : '设置失败')
    }
  }

  const toggleModel = async (id: string) => {
    try {
      await llmModelsApi.toggleModel(id)
      fetchData()
    } catch (e: unknown) {
      alert(e instanceof Error ? e.message : '操作失败')
    }
  }

  return (
    <div>
      <PageHeader title="大模型管理" description="管理大语言模型接入与参数配置">
        <Button onClick={openAddProvider} size="sm">
          <Plus className="w-4 h-4 mr-1" /> 添加供应商
        </Button>
      </PageHeader>

      <Card className="mb-6">
        <CardContent className="py-4">
          {providers.length === 0 && !loading && (
            <p className="text-sm text-gray-400 text-center py-8">暂无供应商，请点击右上角添加</p>
          )}
          <div className="space-y-2">
            {providers.map(p => (
              <div key={p.id} className="border border-gray-100 rounded-lg overflow-hidden">
                <div className="flex items-center gap-3 px-4 py-3 bg-gray-50/50">
                  <button onClick={() => toggleExpand(p.id)} className="text-gray-400 hover:text-gray-600">
                    {expanded.has(p.id) ? <ChevronDown className="w-4 h-4" /> : <ChevronRight className="w-4 h-4" />}
                  </button>
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2">
                      <span className="text-sm font-medium text-gray-900">{p.name}</span>
                      <span className="text-xs px-2 py-0.5 rounded-full bg-gray-100 text-gray-600">{p.type}</span>
                    </div>
                    <p className="text-xs text-gray-400 truncate">{p.baseUrl} · {p.modelCount} 个模型</p>
                  </div>
                  <div className="flex items-center gap-1 flex-shrink-0">
                    <Button variant="ghost" size="sm" onClick={() => openEditProvider(p)} title="编辑">
                      <Edit2 className="w-3.5 h-3.5" />
                    </Button>
                    <Button variant="ghost" size="sm" onClick={() => testConnection(p.id)} isLoading={testingProvider === p.id} title="测试连接">
                      <TestTube className="w-3.5 h-3.5" />
                    </Button>
                    <Button variant="ghost" size="sm" onClick={() => deleteProvider(p.id)} title="删除">
                      <Trash2 className="w-3.5 h-3.5 text-red-500" />
                    </Button>
                    <Button variant="ghost" size="sm" onClick={() => openAddModel(p.id)} title="添加模型">
                      <Plus className="w-3.5 h-3.5" />
                    </Button>
                  </div>
                </div>
                {expanded.has(p.id) && (
                  <div className="px-4 py-2">
                    {p.models?.length === 0 && <p className="text-xs text-gray-400 py-2">暂无模型</p>}
                    <div className="space-y-1">
                      {p.models?.map(m => (
                        <div key={m.id} className="flex items-center gap-3 px-3 py-2 rounded-lg hover:bg-gray-50">
                          <div className="flex-1 min-w-0 grid grid-cols-12 gap-2 items-center">
                            <span className="col-span-3 text-sm text-gray-900 truncate">{m.name}</span>
                            <span className="col-span-2 text-xs text-gray-500">{m.type === 'inference' ? '推理' : '分块'}</span>
                            <span className="col-span-3 text-xs text-gray-500 truncate">{m.modelName}</span>
                            {m.dimension && <span className="col-span-1 text-xs text-gray-400">{m.dimension}维</span>}
                            <span className="col-span-1">
                              {m.isEnabled ? (
                                <span className="text-xs text-emerald-600">已启用</span>
                              ) : (
                                <span className="text-xs text-gray-400">已禁用</span>
                              )}
                            </span>
                            <span className="col-span-1">
                              {m.isDefault && <Star className="w-3.5 h-3.5 text-amber-500 fill-amber-500" />}
                            </span>
                          </div>
                          <div className="flex items-center gap-1 flex-shrink-0">
                            <Button variant="ghost" size="sm" onClick={() => setDefault(m.id)} disabled={m.isDefault} title="设为默认">
                              <Star className={`w-3.5 h-3.5 ${m.isDefault ? 'text-amber-500 fill-amber-500' : 'text-gray-400'}`} />
                            </Button>
                            <Button variant="ghost" size="sm" onClick={() => toggleModel(m.id)} title={m.isEnabled ? '禁用' : '启用'}>
                              {m.isEnabled ? <Power className="w-3.5 h-3.5 text-emerald-500" /> : <PowerOff className="w-3.5 h-3.5 text-gray-400" />}
                            </Button>
                            <Button variant="ghost" size="sm" onClick={() => openEditModel(m, p.id)} title="编辑">
                              <Edit2 className="w-3.5 h-3.5" />
                            </Button>
                            <Button variant="ghost" size="sm" onClick={() => deleteModel(m.id)} title="删除">
                              <Trash2 className="w-3.5 h-3.5 text-red-500" />
                            </Button>
                          </div>
                        </div>
                      ))}
                    </div>
                  </div>
                )}
              </div>
            ))}
          </div>
        </CardContent>
      </Card>

      {embeddings.length > 0 && (
        <Card>
          <CardHeader>
            <h3 className="text-sm font-semibold text-gray-900">内置 Embedding 模型（不可修改）</h3>
          </CardHeader>
          <CardContent>
            <div className="space-y-2">
              {embeddings.map(e => (
                <div key={e.name} className="flex items-center gap-4 px-3 py-2 rounded-lg bg-gray-50">
                  <span className="text-sm text-gray-900 w-40">{e.name}</span>
                  <span className="text-xs text-gray-500">{e.dimension} 维</span>
                  <span className="text-xs text-emerald-600">{e.status}</span>
                  <span className="text-xs text-gray-400">内置，不可修改</span>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
      )}

      {/* Provider Dialog */}
      <Dialog
        open={providerDialog}
        onClose={() => { setProviderDialog(false); resetProviderForm() }}
        title={editingProvider ? '编辑供应商' : '添加供应商'}
        footer={(
          <>
            <Button variant="ghost" onClick={() => { setProviderDialog(false); resetProviderForm() }}>取消</Button>
            {editingProvider && (
              <Button variant="secondary" onClick={() => testConnection(editingProvider)} isLoading={testingProvider === editingProvider}>
                <TestTube className="w-4 h-4 mr-1" /> 测试连接
              </Button>
            )}
            <Button onClick={saveProvider}>保存</Button>
          </>
        )}
      >
        <div className="space-y-4">
          <Input label="供应商名称" value={providerForm.name} onChange={e => setProviderForm(prev => ({ ...prev, name: e.target.value }))} error={providerErrors.name} />
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">接口类型</label>
            <select
              className="w-full px-4 py-2 rounded-lg border border-gray-300 bg-white text-gray-900 focus:outline-none focus:ring-2 focus:ring-blue-500"
              value={providerForm.type}
              onChange={e => setProviderForm(prev => ({ ...prev, type: e.target.value as 'openai' | 'anthropic' }))}
            >
              <option value="openai">OpenAI 兼容接口</option>
              <option value="anthropic">Anthropic 兼容接口</option>
            </select>
          </div>
          <Input label="API Key" type="password" value={providerForm.apiKey} onChange={e => setProviderForm(prev => ({ ...prev, apiKey: e.target.value }))} error={providerErrors.apiKey} />
          <Input label="Base URL" value={providerForm.baseUrl} onChange={e => setProviderForm(prev => ({ ...prev, baseUrl: e.target.value }))} error={providerErrors.baseUrl} />
          <div className="grid grid-cols-2 gap-4">
            <Input label="超时时间（秒）" type="number" value={providerForm.timeout ?? ''} onChange={e => setProviderForm(prev => ({ ...prev, timeout: Number(e.target.value) }))} />
            <Input label="重试次数" type="number" value={providerForm.retryCount ?? ''} onChange={e => setProviderForm(prev => ({ ...prev, retryCount: Number(e.target.value) }))} />
          </div>
        </div>
      </Dialog>

      {/* Model Dialog */}
      <Dialog
        open={modelDialog}
        onClose={() => { setModelDialog(false); resetModelForm() }}
        title={editingModel ? '编辑模型' : '添加模型'}
        footer={(
          <>
            <Button variant="ghost" onClick={() => { setModelDialog(false); resetModelForm() }}>取消</Button>
            <Button onClick={saveModel}>保存</Button>
          </>
        )}
      >
        <div className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">模型类型</label>
            <select
              className="w-full px-4 py-2 rounded-lg border border-gray-300 bg-white text-gray-900 focus:outline-none focus:ring-2 focus:ring-blue-500"
              value={modelForm.type}
              onChange={e => setModelForm(prev => ({ ...prev, type: e.target.value as 'inference' | 'embedding' }))}
            >
              <option value="inference">推理模型</option>
              <option value="embedding">分块模型</option>
            </select>
          </div>
          <Input label="模型名称" value={modelForm.name} onChange={e => setModelForm(prev => ({ ...prev, name: e.target.value }))} error={modelErrors.name} />
          <Input label="实际模型名" value={modelForm.modelName} onChange={e => setModelForm(prev => ({ ...prev, modelName: e.target.value }))} error={modelErrors.modelName} />
          {modelForm.type === 'inference' && (
            <Input label="最大 Token（可选）" type="number" value={modelForm.maxTokens ?? ''} onChange={e => setModelForm(prev => ({ ...prev, maxTokens: e.target.value ? Number(e.target.value) : undefined }))} />
          )}
          {modelForm.type === 'embedding' && (
            <Input label="向量维度" type="number" value={modelForm.dimension ?? ''} onChange={e => setModelForm(prev => ({ ...prev, dimension: Number(e.target.value) }))} error={modelErrors.dimension} />
          )}
          <label className="flex items-center gap-2 text-sm text-gray-700">
            <input
              type="checkbox"
              checked={modelForm.isDefault}
              onChange={e => setModelForm(prev => ({ ...prev, isDefault: e.target.checked }))}
              className="rounded border-gray-300 text-blue-600 focus:ring-blue-500"
            />
            设为默认
          </label>
        </div>
      </Dialog>
    </div>
  )
}
