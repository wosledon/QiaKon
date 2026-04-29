import { useEffect, useState, useCallback } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { PageHeader } from '@/components/shared/PageHeader'
import { Card, CardContent, CardHeader } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { Input } from '@/components/ui/Input'
import { Textarea } from '@/components/ui/Textarea'
import { Dialog } from '@/components/ui/Dialog'
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
} from 'lucide-react'
import { workflowApi } from '@/services/api'
import type { WorkflowDefinition, WorkflowExecution } from '@/types'

function formatDate(iso: string): string {
  return new Date(iso).toLocaleString('zh-CN')
}

export function WorkflowDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
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

  const fetchWorkflow = useCallback(async () => {
    if (!id) return
    setLoading(true)
    try {
      const data = await workflowApi.get(id)
      setWorkflow(data)
      setEditForm({ name: data.name, description: data.description })
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
              onClick={() => setIsEditing(prev => !prev)}
            >
              {isEditing ? (
                <>
                  <X className="w-4 h-4 mr-1" /> 取消
                </>
              ) : (
                <>
                  编辑信息
                </>
              )}
            </Button>
          )}
          <Button size="sm" onClick={() => setExecuteOpen(true)}>
            <Play className="w-4 h-4 mr-1" /> 执行
          </Button>
        </div>
      </PageHeader>

      {isEditing ? (
        <Card className="mb-6">
          <CardHeader>
            <h3 className="text-sm font-semibold text-gray-900">编辑基本信息</h3>
          </CardHeader>
          <CardContent className="space-y-4">
            <Input
              label="名称"
              value={editForm.name}
              onChange={(e) => setEditForm(prev => ({ ...prev, name: e.target.value }))}
            />
            <Textarea
              label="描述"
              value={editForm.description}
              onChange={(e) => setEditForm(prev => ({ ...prev, description: e.target.value }))}
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
                  {workflow.isSystem && (
                    <StatusBadge status="info">系统</StatusBadge>
                  )}
                </div>
                {workflow.description && (
                  <p className="text-sm text-gray-500 mt-1">{workflow.description}</p>
                )}
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
            <Button
              variant="ghost"
              size="sm"
              onClick={() => navigate('/workflows/runs')}
            >
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
                    <p className="text-sm font-medium text-gray-900 truncate">
                      {exec.pipelineName}
                    </p>
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

      <Dialog
        open={executeOpen}
        onClose={() => { setExecuteOpen(false); setExecuteInput('{}') }}
        title="执行工作流"
        footer={(
          <>
            <Button variant="ghost" onClick={() => { setExecuteOpen(false); setExecuteInput('{}') }}>
              取消
            </Button>
            <Button onClick={handleExecute} isLoading={executing}>
              <Play className="w-4 h-4 mr-1" /> 执行
            </Button>
          </>
        )}
      >
        <div className="space-y-3">
          <p className="text-sm text-gray-500">
            输入执行参数（JSON 格式）：
          </p>
          <Textarea
            value={executeInput}
            onChange={(e) => setExecuteInput(e.target.value)}
            rows={6}
            placeholder='{"key": "value"}'
          />
          <p className="text-xs text-gray-400">
            提示：留空或输入 {} 表示无参数执行
          </p>
        </div>
      </Dialog>
    </div>
  )
}
