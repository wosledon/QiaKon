import { useState, useEffect, useCallback } from 'react'
import { useNavigate } from 'react-router-dom'
import { PageHeader } from '@/components/shared/PageHeader'
import { Card, CardContent } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { Input } from '@/components/ui/Input'
import { Textarea } from '@/components/ui/Textarea'
import { Dialog } from '@/components/ui/Dialog'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { GitBranch, Clock, Trash2, RefreshCw, Plus } from 'lucide-react'
import { workflowApi } from '@/services/api'
import type { WorkflowDefinition } from '@/types'

function formatDate(iso: string): string {
  return new Date(iso).toLocaleString('zh-CN')
}

export function WorkflowsPage() {
  const navigate = useNavigate()
  const [workflows, setWorkflows] = useState<WorkflowDefinition[]>([])
  const [loading, setLoading] = useState(false)

  const [createOpen, setCreateOpen] = useState(false)
  const [creating, setCreating] = useState(false)
  const [createForm, setCreateForm] = useState({ name: '', description: '' })
  const [createErrors, setCreateErrors] = useState<{ name?: string }>({})

  const fetchWorkflows = useCallback(async () => {
    setLoading(true)
    try {
      const data = await workflowApi.list()
      setWorkflows(data)
    } catch {
      // ignore
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    fetchWorkflows()
  }, [fetchWorkflows])

  const handleDelete = async (e: React.MouseEvent, id: string) => {
    e.stopPropagation()
    if (!window.confirm('确定删除该工作流？')) return
    try {
      await workflowApi.delete(id)
      setWorkflows((prev) => prev.filter((wf) => wf.id !== id))
    } catch {
      // ignore
    }
  }

  const validateCreate = (): boolean => {
    const errs: { name?: string } = {}
    if (!createForm.name.trim()) errs.name = '请输入工作流名称'
    setCreateErrors(errs)
    return Object.keys(errs).length === 0
  }

  const handleCreate = async () => {
    if (!validateCreate()) return
    setCreating(true)
    try {
      const created = await workflowApi.create({
        name: createForm.name.trim(),
        description: createForm.description.trim(),
      })
      setCreateOpen(false)
      setCreateForm({ name: '', description: '' })
      navigate(`/workflows/${created.id}`)
    } catch (e: unknown) {
      alert(e instanceof Error ? e.message : '创建失败')
    } finally {
      setCreating(false)
    }
  }

  const getStatusBadge = (stageCount: number) => {
    return stageCount > 0 ? 'active' : 'inactive'
  }

  return (
    <div className="mx-auto w-full max-w-7xl p-4 md:p-8">
      <PageHeader title="工作流编排" description="创建和管理 LLM 工作流 pipeline">
        <div className="flex items-center gap-2">
          <Button variant="ghost" size="sm" onClick={fetchWorkflows} isLoading={loading}>
            <RefreshCw className="w-4 h-4" />
          </Button>
          <Button size="sm" onClick={() => setCreateOpen(true)}>
            <Plus className="w-4 h-4 mr-1" /> 新建工作流
          </Button>
        </div>
      </PageHeader>

      {loading && workflows.length === 0 ? (
        <div className="text-center py-12 text-gray-400">加载中...</div>
      ) : workflows.length === 0 ? (
        <div className="text-center py-12 text-gray-400">
          <GitBranch className="w-10 h-10 mx-auto mb-3 opacity-30" />
          <p>暂无工作流</p>
        </div>
      ) : (
        <div className="space-y-3">
          {workflows.map((wf) => (
            <Card
              key={wf.id}
              className="hover:shadow-md transition-shadow cursor-pointer"
              onClick={() => navigate(`/workflows/${wf.id}`)}
            >
              <CardContent className="py-4">
                <div className="flex items-center gap-3 sm:gap-4">
                  <div className="w-10 h-10 rounded-lg bg-purple-50 flex items-center justify-center flex-shrink-0">
                    <GitBranch className="w-5 h-5 text-purple-600" />
                  </div>
                  <div className="flex-1 min-w-0">
                    <p className="text-sm font-medium text-gray-900 truncate">{wf.name}</p>
                    {wf.description && (
                      <p className="text-xs text-gray-500 truncate mt-0.5">{wf.description}</p>
                    )}
                    <div className="flex items-center gap-3 mt-1">
                      <span className="text-xs text-gray-400 flex items-center gap-1">
                        <Clock className="w-3 h-3 flex-shrink-0" />
                        {formatDate(wf.createdAt)}
                      </span>
                      <span className="text-xs text-gray-400">{wf.stageCount} 个阶段</span>
                    </div>
                  </div>
                  <div className="flex items-center gap-2 flex-shrink-0">
                    {wf.isSystem ? (
                      <StatusBadge status="info">系统</StatusBadge>
                    ) : (
                      <>
                        <StatusBadge status={getStatusBadge(wf.stageCount)} />
                        <div className="flex items-center gap-1">
                          <Button
                            variant="ghost"
                            size="sm"
                            className="text-gray-400 hover:text-red-600 px-2"
                            onClick={(e) => handleDelete(e, wf.id)}
                            title="删除工作流"
                          >
                            <Trash2 className="w-4 h-4" />
                          </Button>
                        </div>
                      </>
                    )}
                  </div>
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      <Dialog
        open={createOpen}
        onClose={() => { setCreateOpen(false); setCreateForm({ name: '', description: '' }); setCreateErrors({}) }}
        title="新建工作流"
        footer={(
          <>
            <Button
              variant="ghost"
              onClick={() => { setCreateOpen(false); setCreateForm({ name: '', description: '' }); setCreateErrors({}) }}
            >
              取消
            </Button>
            <Button onClick={handleCreate} isLoading={creating}>
              创建
            </Button>
          </>
        )}
      >
        <div className="space-y-4">
          <Input
            label="名称"
            value={createForm.name}
            onChange={(e) => setCreateForm(prev => ({ ...prev, name: e.target.value }))}
            error={createErrors.name}
            placeholder="输入工作流名称"
          />
          <Textarea
            label="描述"
            value={createForm.description}
            onChange={(e) => setCreateForm(prev => ({ ...prev, description: e.target.value }))}
            placeholder="输入工作流描述（可选）"
            rows={3}
          />
        </div>
      </Dialog>
    </div>
  )
}
