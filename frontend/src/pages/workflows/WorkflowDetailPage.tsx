import { useEffect, useState, useCallback } from 'react'
import { useNavigate, useParams, useSearchParams } from 'react-router-dom'
import { PageHeader } from '@/components/shared/PageHeader'
import { Card, CardContent, CardHeader } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { Input } from '@/components/ui/Input'
import { Textarea } from '@/components/ui/Textarea'
import { Dialog } from '@/components/ui/Dialog'
import { Select } from '@/components/ui/Select'
import { StatusBadge } from '@/components/shared/StatusBadge'
import {
  ArrowLeft,
  Play,
  Save,
  X,
  GitBranch,
  Clock,
  Layers,
  CheckCircle2,
  AlertCircle,
  Loader2,
  ChevronRight,
  Settings2,
  Info,
  Plus,
  Trash2,
  ArrowUp,
  ArrowDown,
  GripVertical,
} from 'lucide-react'
import { workflowApi } from '@/services/api'
import type { WorkflowDefinition, WorkflowExecution, WorkflowStageConfig } from '@/types'

function formatDate(iso: string): string {
  return new Date(iso).toLocaleString('zh-CN')
}

function generateId(): string {
  return Math.random().toString(36).slice(2, 9)
}

const STAGE_MODES = [
  { value: 'sequential', label: '顺序执行' },
  { value: 'parallel', label: '并行执行' },
  { value: 'fallback', label: '容错回退' },
]

const STEP_TYPES = [
  { value: 'llm', label: 'LLM 调用' },
  { value: 'retrieval', label: '检索增强' },
  { value: 'graph', label: '图谱查询' },
  { value: 'transform', label: '数据转换' },
  { value: 'condition', label: '条件分支' },
  { value: 'custom', label: '自定义' },
]

interface EditableStage {
  id: string
  name: string
  mode: 'sequential' | 'parallel' | 'fallback'
  steps: EditableStep[]
}

interface EditableStep {
  id: string
  name: string
  type: 'llm' | 'retrieval' | 'graph' | 'transform' | 'condition' | 'custom'
  config?: Record<string, unknown>
}

function parseStagesFromWorkflow(wf: WorkflowDefinition | null): EditableStage[] {
  if (!wf) return []
  const raw = wf.config?.stages as WorkflowStageConfig[] | undefined
  if (raw && Array.isArray(raw) && raw.length > 0) {
    return raw.map((s) => ({
      id: generateId(),
      name: s.name,
      mode: (s.mode as EditableStage['mode']) || 'sequential',
      steps: (s.steps || []).map((step) => ({
        id: generateId(),
        name: step.name,
        type: (step.type as EditableStep['type']) || 'custom',
        config: step.config,
      })),
    }))
  }
  if (wf.stages && wf.stages.length > 0) {
    return wf.stages.map((s) => ({
      id: generateId(),
      name: s.name,
      mode: 'sequential',
      steps: Array.from({ length: s.stepCount }, (_, i) => ({
        id: generateId(),
        name: `步骤 ${i + 1}`,
        type: 'custom' as EditableStep['type'],
      })),
    }))
  }
  return []
}

export function WorkflowDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const [searchParams, setSearchParams] = useSearchParams()

  const initialTab = searchParams.get('tab') === 'orchestrate' ? 'orchestrate' : 'details'
  const [activeTab, setActiveTab] = useState<'details' | 'orchestrate'>(initialTab)

  const [workflow, setWorkflow] = useState<WorkflowDefinition | null>(null)
  const [loading, setLoading] = useState(false)
  const [saving, setSaving] = useState(false)
  const [isEditing, setIsEditing] = useState(false)

  const [editForm, setEditForm] = useState({
    name: '',
    description: '',
  })

  const [executeOpen, setExecuteOpen] = useState(false)
  const [executeInput, setExecuteInput] = useState('{}')
  const [executing, setExecuting] = useState(false)

  const [executions, setExecutions] = useState<WorkflowExecution[]>([])
  const [executionsLoading, setExecutionsLoading] = useState(false)

  // Orchestration state
  const [stages, setStages] = useState<EditableStage[]>([])
  const [expandedStageId, setExpandedStageId] = useState<string | null>(null)
  const [orchestrateSaving, setOrchestrateSaving] = useState(false)

  const fetchWorkflow = useCallback(async () => {
    if (!id) return
    setLoading(true)
    try {
      const data = await workflowApi.get(id)
      setWorkflow(data)
      setEditForm({ name: data.name, description: data.description })
      setStages(parseStagesFromWorkflow(data))
    } catch {
      // ignore
    } finally {
      setLoading(false)
    }
  }, [id])

  const fetchExecutions = useCallback(async () => {
    if (!id) return
    setExecutionsLoading(true)
    try {
      const data = await workflowApi.getExecutions(1, 10, id)
      setExecutions(data.items)
    } catch {
      // ignore
    } finally {
      setExecutionsLoading(false)
    }
  }, [id])

  useEffect(() => {
    fetchWorkflow()
    fetchExecutions()
  }, [fetchWorkflow, fetchExecutions])

  useEffect(() => {
    const tab = searchParams.get('tab') === 'orchestrate' ? 'orchestrate' : 'details'
    setActiveTab(tab)
  }, [searchParams])

  const handleTabChange = (tab: 'details' | 'orchestrate') => {
    setActiveTab(tab)
    setSearchParams(tab === 'details' ? {} : { tab: 'orchestrate' }, { replace: true })
  }

  const handleSave = async () => {
    if (!id || !editForm.name.trim()) return
    setSaving(true)
    try {
      const updated = await workflowApi.update(id, {
        name: editForm.name,
        description: editForm.description,
      })
      setWorkflow(updated)
      setIsEditing(false)
    } catch (e: unknown) {
      alert(e instanceof Error ? e.message : '保存失败')
    } finally {
      setSaving(false)
    }
  }

  const handleSaveOrchestration = async () => {
    if (!id) return
    setOrchestrateSaving(true)
    try {
      const payload: WorkflowStageConfig[] = stages.map((s) => ({
        name: s.name,
        mode: s.mode,
        steps: s.steps.map((step) => ({
          name: step.name,
          type: step.type,
          config: step.config,
        })),
      }))
      const updated = await workflowApi.update(id, {
        config: {
          ...workflow?.config,
          stages: payload,
        },
      })
      setWorkflow(updated)
      setStages(parseStagesFromWorkflow(updated))
      alert('编排已保存')
    } catch (e: unknown) {
      alert(e instanceof Error ? e.message : '保存失败')
    } finally {
      setOrchestrateSaving(false)
    }
  }

  const handleExecute = async () => {
    if (!id) return
    let parsedInput: Record<string, unknown> = {}
    try {
      parsedInput = JSON.parse(executeInput)
    } catch {
      alert('输入参数 JSON 格式不正确')
      return
    }
    setExecuting(true)
    try {
      const result = await workflowApi.execute(id, parsedInput)
      setExecuteOpen(false)
      setExecuteInput('{}')
      alert(`工作流已启动，执行ID: ${result.executionId}`)
      fetchExecutions()
      navigate(`/workflows/runs`)
    } catch (e: unknown) {
      alert(e instanceof Error ? e.message : '执行失败')
    } finally {
      setExecuting(false)
    }
  }

  // Orchestration helpers
  const addStage = () => {
    const newStage: EditableStage = {
      id: generateId(),
      name: `阶段 ${stages.length + 1}`,
      mode: 'sequential',
      steps: [],
    }
    setStages((prev) => [...prev, newStage])
    setExpandedStageId(newStage.id)
  }

  const removeStage = (stageId: string) => {
    setStages((prev) => prev.filter((s) => s.id !== stageId))
  }

  const moveStage = (index: number, direction: -1 | 1) => {
    const newIndex = index + direction
    if (newIndex < 0 || newIndex >= stages.length) return
    setStages((prev) => {
      const next = [...prev]
      const [removed] = next.splice(index, 1)
      next.splice(newIndex, 0, removed)
      return next
    })
  }

  const updateStage = (stageId: string, patch: Partial<EditableStage>) => {
    setStages((prev) => prev.map((s) => (s.id === stageId ? { ...s, ...patch } : s)))
  }

  const addStep = (stageId: string) => {
    setStages((prev) =>
      prev.map((s) =>
        s.id === stageId
          ? {
              ...s,
              steps: [
                ...s.steps,
                {
                  id: generateId(),
                  name: `步骤 ${s.steps.length + 1}`,
                  type: 'custom',
                },
              ],
            }
          : s
      )
    )
  }

  const removeStep = (stageId: string, stepId: string) => {
    setStages((prev) =>
      prev.map((s) => (s.id === stageId ? { ...s, steps: s.steps.filter((step) => step.id !== stepId) } : s))
    )
  }

  const moveStep = (stageId: string, index: number, direction: -1 | 1) => {
    setStages((prev) =>
      prev.map((s) => {
        if (s.id !== stageId) return s
        const newIndex = index + direction
        if (newIndex < 0 || newIndex >= s.steps.length) return s
        const next = [...s.steps]
        const [removed] = next.splice(index, 1)
        next.splice(newIndex, 0, removed)
        return { ...s, steps: next }
      })
    )
  }

  const updateStep = (stageId: string, stepId: string, patch: Partial<EditableStep>) => {
    setStages((prev) =>
      prev.map((s) =>
        s.id === stageId ? { ...s, steps: s.steps.map((step) => (step.id === stepId ? { ...step, ...patch } : step)) } : s
      )
    )
  }

  const getStatusBadge = (status: string) => {
    switch (status.toLowerCase()) {
      case 'completed':
        return 'success'
      case 'failed':
        return 'error'
      case 'running':
        return 'warning'
      default:
        return 'default'
    }
  }

  const formatDuration = (ms: number | null) => {
    if (!ms) return '-'
    if (ms < 1000) return `${ms}ms`
    return `${(ms / 1000).toFixed(1)}s`
  }

  if (loading && !workflow) {
    return (
      <div className="mx-auto w-full max-w-7xl p-4 md:p-8">
        <div className="text-center py-12 text-gray-400">
          <Loader2 className="w-8 h-8 animate-spin mx-auto mb-3" />
          <p>加载中...</p>
        </div>
      </div>
    )
  }

  if (!workflow) {
    return (
      <div className="mx-auto w-full max-w-7xl p-4 md:p-8">
        <div className="text-center py-12 text-gray-400">
          <AlertCircle className="w-10 h-10 mx-auto mb-3 opacity-30" />
          <p>工作流不存在或加载失败</p>
          <Button variant="ghost" size="sm" className="mt-4" onClick={() => navigate('/workflows')}>
            <ArrowLeft className="w-4 h-4 mr-1" /> 返回列表
          </Button>
        </div>
      </div>
    )
  }

  return (
    <div className="mx-auto w-full max-w-7xl p-4 md:p-8">
      <PageHeader
        title={workflow.name}
        description={workflow.description || '工作流详情'}
      >
        <div className="flex items-center gap-2">
          <Button variant="ghost" size="sm" onClick={() => navigate('/workflows')}>
            <ArrowLeft className="w-4 h-4 mr-1" /> 返回
          </Button>
          {!workflow.isSystem && (
            <Button
              variant="secondary"
              size="sm"
              onClick={() => setIsEditing((prev) => !prev)}
            >
              {isEditing ? (
                <>
                  <X className="w-4 h-4 mr-1" /> 取消
                </>
              ) : (
                <>编辑信息</>
              )}
            </Button>
          )}
          <Button size="sm" onClick={() => setExecuteOpen(true)}>
            <Play className="w-4 h-4 mr-1" /> 执行
          </Button>
        </div>
      </PageHeader>

      {/* Tabs */}
      <div className="flex items-center gap-1 mb-6 border-b border-gray-200">
        <button
          onClick={() => handleTabChange('details')}
          className={`px-4 py-2 text-sm font-medium border-b-2 transition-colors ${
            activeTab === 'details'
              ? 'border-purple-600 text-purple-700'
              : 'border-transparent text-gray-500 hover:text-gray-700'
          }`}
        >
          <span className="flex items-center gap-1.5">
            <Info className="w-4 h-4" /> 详情
          </span>
        </button>
        <button
          onClick={() => handleTabChange('orchestrate')}
          className={`px-4 py-2 text-sm font-medium border-b-2 transition-colors ${
            activeTab === 'orchestrate'
              ? 'border-purple-600 text-purple-700'
              : 'border-transparent text-gray-500 hover:text-gray-700'
          }`}
        >
          <span className="flex items-center gap-1.5">
            <Settings2 className="w-4 h-4" /> 编排
          </span>
        </button>
      </div>

      {activeTab === 'details' ? (
        <>
          {isEditing ? (
            <Card className="mb-6">
              <CardHeader>
                <h3 className="text-sm font-semibold text-gray-900">编辑基本信息</h3>
              </CardHeader>
              <CardContent className="space-y-4">
                <Input
                  label="名称"
                  value={editForm.name}
                  onChange={(e) => setEditForm((prev) => ({ ...prev, name: e.target.value }))}
                />
                <Textarea
                  label="描述"
                  value={editForm.description}
                  onChange={(e) => setEditForm((prev) => ({ ...prev, description: e.target.value }))}
                  rows={3}
                />
                <div className="flex justify-end gap-2">
                  <Button variant="ghost" size="sm" onClick={() => setIsEditing(false)}>
                    取消
                  </Button>
                  <Button size="sm" onClick={handleSave} isLoading={saving}>
                    <Save className="w-4 h-4 mr-1" /> 保存
                  </Button>
                </div>
              </CardContent>
            </Card>
          ) : (
            <Card className="mb-6">
              <CardContent className="py-4">
                <div className="flex items-start gap-4">
                  <div className="w-12 h-12 rounded-xl bg-purple-50 flex items-center justify-center flex-shrink-0">
                    <GitBranch className="w-6 h-6 text-purple-600" />
                  </div>
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-3 flex-wrap">
                      <h2 className="text-lg font-semibold text-gray-900">{workflow.name}</h2>
                      {workflow.isSystem && <StatusBadge status="info">系统</StatusBadge>}
                    </div>
                    {workflow.description && <p className="text-sm text-gray-500 mt-1">{workflow.description}</p>}
                    <div className="flex items-center gap-4 mt-3 text-xs text-gray-400 flex-wrap">
                      <span className="flex items-center gap-1">
                        <Layers className="w-3.5 h-3.5" />
                        {workflow.stageCount} 个阶段
                      </span>
                      <span className="flex items-center gap-1">
                        <Clock className="w-3.5 h-3.5" />
                        创建于 {formatDate(workflow.createdAt)}
                      </span>
                      {workflow.updatedAt && (
                        <span className="flex items-center gap-1">
                          <Clock className="w-3.5 h-3.5" />
                          更新于 {formatDate(workflow.updatedAt)}
                        </span>
                      )}
                    </div>
                  </div>
                </div>
              </CardContent>
            </Card>
          )}

          {workflow.stages && workflow.stages.length > 0 && (
            <Card className="mb-6">
              <CardHeader>
                <h3 className="text-sm font-semibold text-gray-900">阶段编排</h3>
              </CardHeader>
              <CardContent>
                <div className="space-y-2">
                  {workflow.stages.map((stage, index) => (
                    <div
                      key={index}
                      className="flex items-center gap-3 px-4 py-3 rounded-lg border border-gray-100 bg-gray-50/50"
                    >
                      <div className="w-6 h-6 rounded-full bg-purple-100 text-purple-700 flex items-center justify-center text-xs font-medium flex-shrink-0">
                        {index + 1}
                      </div>
                      <div className="flex-1 min-w-0">
                        <div className="flex items-center gap-2">
                          <span className="text-sm font-medium text-gray-900">{stage.name}</span>
                          <StatusBadge status="info">{stage.mode}</StatusBadge>
                        </div>
                        <p className="text-xs text-gray-400 mt-0.5">{stage.stepCount} 个步骤</p>
                      </div>
                      {workflow.stages && index < workflow.stages.length - 1 && (
                        <ChevronRight className="w-4 h-4 text-gray-300 flex-shrink-0" />
                      )}
                    </div>
                  ))}
                </div>
              </CardContent>
            </Card>
          )}

          <Card>
            <CardHeader>
              <div className="flex items-center justify-between">
                <h3 className="text-sm font-semibold text-gray-900">最近执行记录</h3>
                <Button variant="ghost" size="sm" onClick={() => navigate('/workflows/runs')}>
                  查看全部
                </Button>
              </div>
            </CardHeader>
            <CardContent>
              {executionsLoading && executions.length === 0 ? (
                <p className="text-sm text-gray-400 text-center py-6">加载中...</p>
              ) : executions.length === 0 ? (
                <p className="text-sm text-gray-400 text-center py-6">暂无执行记录</p>
              ) : (
                <div className="space-y-2">
                  {executions.map((exec) => (
                    <div
                      key={exec.id}
                      className="flex items-center gap-3 px-4 py-3 rounded-lg border border-gray-100 hover:bg-gray-50 cursor-pointer transition-colors"
                      onClick={() => navigate('/workflows/runs')}
                    >
                      <div className="flex-shrink-0">
                        {exec.status === 'Completed' ? (
                          <CheckCircle2 className="w-5 h-5 text-emerald-500" />
                        ) : exec.status === 'Failed' ? (
                          <AlertCircle className="w-5 h-5 text-rose-500" />
                        ) : (
                          <Loader2 className="w-5 h-5 text-amber-500 animate-spin" />
                        )}
                      </div>
                      <div className="flex-1 min-w-0">
                        <p className="text-sm font-medium text-gray-900 truncate">{exec.pipelineName}</p>
                        <p className="text-xs text-gray-400">
                          {formatDate(exec.startedAt)} · 耗时 {formatDuration(exec.duration)}
                        </p>
                      </div>
                      <StatusBadge status={getStatusBadge(exec.status)} />
                    </div>
                  ))}
                </div>
              )}
            </CardContent>
          </Card>
        </>
      ) : (
        /* Orchestrate Tab */
        <div className="space-y-6">
          <Card>
            <CardHeader>
              <div className="flex items-center justify-between">
                <div>
                  <h3 className="text-sm font-semibold text-gray-900">阶段与步骤编排</h3>
                  <p className="text-xs text-gray-400 mt-0.5">
                    配置工作流的执行阶段和每个阶段内的处理步骤
                  </p>
                </div>
                <div className="flex items-center gap-2">
                  <Button size="sm" variant="secondary" onClick={addStage}>
                    <Plus className="w-4 h-4 mr-1" /> 添加阶段
                  </Button>
                  <Button size="sm" onClick={handleSaveOrchestration} isLoading={orchestrateSaving}>
                    <Save className="w-4 h-4 mr-1" /> 保存编排
                  </Button>
                </div>
              </div>
            </CardHeader>
            <CardContent>
              {stages.length === 0 ? (
                <div className="text-center py-10 text-gray-400">
                  <Layers className="w-10 h-10 mx-auto mb-3 opacity-30" />
                  <p>暂无阶段配置</p>
                  <p className="text-xs mt-1">点击上方「添加阶段」开始编排工作流</p>
                </div>
              ) : (
                <div className="space-y-4">
                  {stages.map((stage, stageIndex) => (
                    <div
                      key={stage.id}
                      className="rounded-lg border border-gray-200 bg-white overflow-hidden"
                    >
                      {/* Stage Header */}
                      <div
                        className="flex items-center gap-3 px-4 py-3 bg-gray-50/80 cursor-pointer select-none"
                        onClick={() =>
                          setExpandedStageId((prev) => (prev === stage.id ? null : stage.id))
                        }
                      >
                        <GripVertical className="w-4 h-4 text-gray-300 flex-shrink-0" />
                        <div className="w-6 h-6 rounded-full bg-purple-100 text-purple-700 flex items-center justify-center text-xs font-medium flex-shrink-0">
                          {stageIndex + 1}
                        </div>
                        <div className="flex-1 min-w-0">
                          <Input
                            value={stage.name}
                            onChange={(e) => updateStage(stage.id, { name: e.target.value })}
                            onClick={(e) => e.stopPropagation()}
                            className="text-sm font-medium bg-white"
                            placeholder="阶段名称"
                          />
                        </div>
                        <div className="w-40 flex-shrink-0" onClick={(e) => e.stopPropagation()}>
                          <Select
                            value={stage.mode}
                            options={STAGE_MODES}
                            onChange={(e) =>
                              updateStage(stage.id, { mode: e.target.value as EditableStage['mode'] })
                            }
                            className="text-sm py-1.5"
                          />
                        </div>
                        <div className="flex items-center gap-1 flex-shrink-0" onClick={(e) => e.stopPropagation()}>
                          <Button
                            variant="ghost"
                            size="sm"
                            className="px-1.5 text-gray-400 hover:text-gray-600"
                            onClick={() => moveStage(stageIndex, -1)}
                            disabled={stageIndex === 0}
                          >
                            <ArrowUp className="w-3.5 h-3.5" />
                          </Button>
                          <Button
                            variant="ghost"
                            size="sm"
                            className="px-1.5 text-gray-400 hover:text-gray-600"
                            onClick={() => moveStage(stageIndex, 1)}
                            disabled={stageIndex === stages.length - 1}
                          >
                            <ArrowDown className="w-3.5 h-3.5" />
                          </Button>
                          <Button
                            variant="ghost"
                            size="sm"
                            className="px-1.5 text-gray-400 hover:text-red-600"
                            onClick={() => removeStage(stage.id)}
                          >
                            <Trash2 className="w-3.5 h-3.5" />
                          </Button>
                        </div>
                      </div>

                      {/* Steps */}
                      {expandedStageId === stage.id && (
                        <div className="px-4 py-3 border-t border-gray-100">
                          {stage.steps.length === 0 ? (
                            <p className="text-xs text-gray-400 text-center py-4">该阶段暂无步骤</p>
                          ) : (
                            <div className="space-y-2">
                              {stage.steps.map((step, stepIndex) => (
                                <div
                                  key={step.id}
                                  className="flex items-center gap-3 px-3 py-2 rounded-md border border-gray-100 bg-gray-50/40"
                                >
                                  <GripVertical className="w-3.5 h-3.5 text-gray-300 flex-shrink-0" />
                                  <div className="w-5 h-5 rounded-full bg-gray-200 text-gray-600 flex items-center justify-center text-[10px] font-medium flex-shrink-0">
                                    {stepIndex + 1}
                                  </div>
                                  <div className="flex-1 min-w-0">
                                    <Input
                                      value={step.name}
                                      onChange={(e) => updateStep(stage.id, step.id, { name: e.target.value })}
                                      className="text-sm py-1.5"
                                      placeholder="步骤名称"
                                    />
                                  </div>
                                  <div className="w-36 flex-shrink-0">
                                    <Select
                                      value={step.type}
                                      options={STEP_TYPES}
                                      onChange={(e) =>
                                        updateStep(stage.id, step.id, {
                                          type: e.target.value as EditableStep['type'],
                                        })
                                      }
                                      className="text-sm py-1.5"
                                    />
                                  </div>
                                  <div className="flex items-center gap-1 flex-shrink-0">
                                    <Button
                                      variant="ghost"
                                      size="sm"
                                      className="px-1.5 text-gray-400 hover:text-gray-600"
                                      onClick={() => moveStep(stage.id, stepIndex, -1)}
                                      disabled={stepIndex === 0}
                                    >
                                      <ArrowUp className="w-3 h-3" />
                                    </Button>
                                    <Button
                                      variant="ghost"
                                      size="sm"
                                      className="px-1.5 text-gray-400 hover:text-gray-600"
                                      onClick={() => moveStep(stage.id, stepIndex, 1)}
                                      disabled={stepIndex === stage.steps.length - 1}
                                    >
                                      <ArrowDown className="w-3 h-3" />
                                    </Button>
                                    <Button
                                      variant="ghost"
                                      size="sm"
                                      className="px-1.5 text-gray-400 hover:text-red-600"
                                      onClick={() => removeStep(stage.id, step.id)}
                                    >
                                      <Trash2 className="w-3 h-3" />
                                    </Button>
                                  </div>
                                </div>
                              ))}
                            </div>
                          )}
                          <div className="mt-3">
                            <Button size="sm" variant="ghost" onClick={() => addStep(stage.id)}>
                              <Plus className="w-3.5 h-3.5 mr-1" /> 添加步骤
                            </Button>
                          </div>
                        </div>
                      )}
                    </div>
                  ))}
                </div>
              )}
            </CardContent>
          </Card>
        </div>
      )}

      <Dialog
        open={executeOpen}
        onClose={() => {
          setExecuteOpen(false)
          setExecuteInput('{}')
        }}
        title="执行工作流"
        footer={
          <>
            <Button
              variant="ghost"
              onClick={() => {
                setExecuteOpen(false)
                setExecuteInput('{}')
              }}
            >
              取消
            </Button>
            <Button onClick={handleExecute} isLoading={executing}>
              <Play className="w-4 h-4 mr-1" /> 执行
            </Button>
          </>
        }
      >
        <div className="space-y-3">
          <p className="text-sm text-gray-500">输入执行参数（JSON 格式）：</p>
          <Textarea
            value={executeInput}
            onChange={(e) => setExecuteInput(e.target.value)}
            rows={6}
            placeholder='{"key": "value"}'
          />
          <p className="text-xs text-gray-400">提示：留空或输入 {} 表示无参数执行</p>
        </div>
      </Dialog>
    </div>
  )
}
