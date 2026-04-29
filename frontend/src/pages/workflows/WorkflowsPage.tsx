import { useState, useEffect, useCallback } from 'react'
import { useNavigate } from 'react-router-dom'
import { PageHeader } from '@/components/shared/PageHeader'
import { Card, CardContent } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { GitBranch, Clock, Trash2, RefreshCw } from 'lucide-react'
import { workflowApi } from '@/services/api'
import type { WorkflowDefinition } from '@/types'

function formatDate(iso: string): string {
  return new Date(iso).toLocaleString('zh-CN')
}

export function WorkflowsPage() {
  const navigate = useNavigate()
  const [workflows, setWorkflows] = useState<WorkflowDefinition[]>([])
  const [loading, setLoading] = useState(false)

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
    try {
      await workflowApi.delete(id)
      setWorkflows((prev) => prev.filter((wf) => wf.id !== id))
    } catch {
      // ignore
    }
  }

  const getStatusBadge = (stageCount: number) => {
    return stageCount > 0 ? 'active' : 'inactive'
  }

  return (
    <div className="mx-auto w-full max-w-7xl p-4 md:p-8">
      <PageHeader title="工作流编排" description="创建和管理 LLM 工作流 pipeline">
        <Button variant="ghost" size="sm" onClick={fetchWorkflows} isLoading={loading}>
          <RefreshCw className="w-4 h-4" />
        </Button>
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
                  </div>
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}
    </div>
  )
}
