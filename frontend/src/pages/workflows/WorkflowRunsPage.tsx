import { useState, useEffect, useCallback } from 'react'
import { PageHeader } from '@/components/shared/PageHeader'
import { Card, CardContent, CardHeader } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { StatusBadge } from '@/components/shared/StatusBadge'
import {
  Play,
  Clock,
  AlertCircle,
  X,
  ChevronRight,
  CheckCircle2,
  Loader2,
  Layers,
  Terminal,
  FileJson,
} from 'lucide-react'
import { workflowApi } from '@/services/api'
import type { WorkflowExecution, WorkflowExecutionDetail, StageResultDetail, StepResultDetail } from '@/types'

export function WorkflowRunsPage() {
  const [executions, setExecutions] = useState<WorkflowExecution[]>([])
  const [loading, setLoading] = useState(false)

  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [detail, setDetail] = useState<WorkflowExecutionDetail | null>(null)
  const [detailLoading, setDetailLoading] = useState(false)
  const [detailError, setDetailError] = useState<string | null>(null)

  const [executionInput, setExecutionInput] = useState<Record<string, unknown> | null>(null)

  const fetchExecutions = useCallback(async () => {
    setLoading(true)
    try {
      const data = await workflowApi.getExecutions(1, 50)
      setExecutions(data.items)
    } catch {
      // ignore
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    fetchExecutions()
    const timer = setInterval(fetchExecutions, 5000)
    return () => clearInterval(timer)
  }, [fetchExecutions])

  const fetchDetail = useCallback(async (id: string) => {
    setDetailLoading(true)
    setDetailError(null)
    try {
      const [resultRes, inputRes] = await Promise.all([
        workflowApi.getExecutionResult(id).catch(() => null),
        workflowApi.getExecutionInput(id).catch(() => null),
      ])
      if (resultRes) {
        setDetail(resultRes)
      } else {
        setDetailError('执行结果未就绪或已过期')
      }
      if (inputRes) {
        setExecutionInput(inputRes.input)
      }
    } catch {
      setDetailError('加载详情失败')
    } finally {
      setDetailLoading(false)
    }
  }, [])

  const handleSelect = (id: string) => {
    if (selectedId === id) {
      setSelectedId(null)
      setDetail(null)
      setExecutionInput(null)
      setDetailError(null)
      return
    }
    setSelectedId(id)
    fetchDetail(id)
  }

  const formatDuration = (ms: number | null) => {
    if (!ms) return '-'
    if (ms < 1000) return `${ms}ms`
    return `${(ms / 1000).toFixed(1)}s`
  }

  const formatTime = (dateStr: string) => {
    return new Date(dateStr).toLocaleString('zh-CN')
  }

  const getStatusBadge = (status: string) => {
    switch (status.toLowerCase()) {
      case 'completed': return 'success'
      case 'failed': return 'error'
      case 'running': return 'warning'
      default: return 'default'
    }
  }

  const getStepIcon = (status: string) => {
    switch (status.toLowerCase()) {
      case 'succeeded':
        return <CheckCircle2 className="w-4 h-4 text-emerald-500" />
      case 'failed':
        return <AlertCircle className="w-4 h-4 text-rose-500" />
      default:
        return <Loader2 className="w-4 h-4 text-amber-500 animate-spin" />
    }
  }

  return (
    <div className="mx-auto w-full max-w-7xl p-4 md:p-8 flex gap-6">
      <div className={`transition-all duration-300 ${selectedId ? 'w-1/2' : 'w-full'}`}>
        <PageHeader title="运行记录" description="查看工作流执行历史与结果" />

        {loading && executions.length === 0 ? (
          <div className="text-center py-12 text-gray-400">加载中...</div>
        ) : executions.length === 0 ? (
          <div className="text-center py-12 text-gray-400">
            <Play className="w-10 h-10 mx-auto mb-3 opacity-30" />
            <p>暂无执行记录</p>
          </div>
        ) : (
          <div className="space-y-3">
            {executions.map((exec) => (
              <Card
                key={exec.id}
                className={`hover:shadow-md transition-all cursor-pointer ${
                  selectedId === exec.id ? 'ring-2 ring-blue-500 border-blue-500' : ''
                }`}
                onClick={() => handleSelect(exec.id)}
              >
                <CardContent className="py-4">
                  <div className="flex items-center gap-3 sm:gap-4">
                    <div className={`w-10 h-10 rounded-lg flex items-center justify-center flex-shrink-0 ${
                      exec.status === 'Completed' ? 'bg-green-50' : exec.status === 'Failed' ? 'bg-red-50' : 'bg-amber-50'
                    }`}>
                      {exec.status === 'Failed' ? (
                        <AlertCircle className="w-5 h-5 text-red-600" />
                      ) : (
                        <Play className={`w-5 h-5 ${exec.status === 'Completed' ? 'text-green-600' : 'text-amber-600'}`} />
                      )}
                    </div>
                    <div className="flex-1 min-w-0">
                      <p className="text-sm font-medium text-gray-900 truncate">{exec.pipelineName}</p>
                      <div className="flex items-center gap-3 mt-1">
                        <span className="text-xs text-gray-400 flex items-center gap-1">
                          <Clock className="w-3 h-3 flex-shrink-0" />
                          {formatTime(exec.startedAt)}
                        </span>
                        <span className="text-xs text-gray-400">耗时 {formatDuration(exec.duration)}</span>
                      </div>
                    </div>
                    <div className="flex items-center gap-3 flex-shrink-0">
                      <StatusBadge status={getStatusBadge(exec.status)} />
                      <ChevronRight className={`w-4 h-4 text-gray-400 transition-transform ${selectedId === exec.id ? 'rotate-90' : ''}`} />
                    </div>
                  </div>
                  {exec.error && (
                    <p className="mt-2 text-xs text-red-600 bg-red-50 rounded px-2 py-1 truncate">{exec.error}</p>
                  )}
                </CardContent>
              </Card>
            ))}
          </div>
        )}
      </div>

      {selectedId && (
        <div className="w-1/2 border-l border-gray-200 pl-6">
          <div className="sticky top-4">
            <div className="flex items-center justify-between mb-4">
              <h2 className="text-lg font-semibold text-gray-900">执行详情</h2>
              <Button variant="ghost" size="sm" onClick={() => handleSelect(selectedId)}>
                <X className="w-4 h-4" />
              </Button>
            </div>

            {detailLoading ? (
              <div className="text-center py-12 text-gray-400">
                <Loader2 className="w-8 h-8 animate-spin mx-auto mb-3" />
                <p>加载详情中...</p>
              </div>
            ) : detailError ? (
              <div className="text-center py-12 text-gray-400">
                <AlertCircle className="w-8 h-8 mx-auto mb-3 opacity-30" />
                <p>{detailError}</p>
                <Button variant="ghost" size="sm" className="mt-4" onClick={() => fetchDetail(selectedId)}>
                  重试
                </Button>
              </div>
            ) : detail ? (
              <div className="space-y-4">
                <Card>
                  <CardContent className="py-4">
                    <div className="flex items-center gap-3 mb-3">
                      {detail.isSuccess ? (
                        <CheckCircle2 className="w-6 h-6 text-emerald-500" />
                      ) : (
                        <AlertCircle className="w-6 h-6 text-rose-500" />
                      )}
                      <div>
                        <p className="text-sm font-medium text-gray-900">{detail.pipelineName}</p>
                        <p className="text-xs text-gray-400">ID: {detail.executionId}</p>
                      </div>
                    </div>
                    <div className="flex items-center gap-4 text-xs text-gray-500 flex-wrap">
                      <span className="flex items-center gap-1">
                        <Clock className="w-3.5 h-3.5" />
                        总耗时 {formatDuration(detail.totalDurationMs)}
                      </span>
                      <StatusBadge status={getStatusBadge(detail.status)} />
                    </div>
                    {detail.error && (
                      <div className="mt-3 p-3 bg-red-50 rounded-lg">
                        <p className="text-xs font-medium text-red-700 mb-1">错误信息</p>
                        <p className="text-xs text-red-600 whitespace-pre-wrap">{detail.error}</p>
                      </div>
                    )}
                  </CardContent>
                </Card>

                {executionInput && Object.keys(executionInput).length > 0 && (
                  <Card>
                    <CardHeader>
                      <div className="flex items-center gap-2">
                        <FileJson className="w-4 h-4 text-gray-500" />
                        <h3 className="text-sm font-semibold text-gray-900">执行输入</h3>
                      </div>
                    </CardHeader>
                    <CardContent>
                      <pre className="text-xs bg-gray-50 rounded-lg p-3 overflow-auto max-h-40 text-gray-700">
                        {JSON.stringify(executionInput, null, 2)}
                      </pre>
                    </CardContent>
                  </Card>
                )}

                {detail.output && Object.keys(detail.output).length > 0 && (
                  <Card>
                    <CardHeader>
                      <div className="flex items-center gap-2">
                        <Terminal className="w-4 h-4 text-gray-500" />
                        <h3 className="text-sm font-semibold text-gray-900">执行输出</h3>
                      </div>
                    </CardHeader>
                    <CardContent>
                      <pre className="text-xs bg-gray-50 rounded-lg p-3 overflow-auto max-h-40 text-gray-700">
                        {JSON.stringify(detail.output, null, 2)}
                      </pre>
                    </CardContent>
                  </Card>
                )}

                {detail.stageResults && detail.stageResults.length > 0 && (
                  <Card>
                    <CardHeader>
                      <div className="flex items-center gap-2">
                        <Layers className="w-4 h-4 text-gray-500" />
                        <h3 className="text-sm font-semibold text-gray-900">阶段结果</h3>
                      </div>
                    </CardHeader>
                    <CardContent className="space-y-3">
                      {detail.stageResults.map((stage: StageResultDetail, idx: number) => (
                        <div key={idx} className="border border-gray-100 rounded-lg p-3">
                          <div className="flex items-center justify-between mb-2">
                            <div className="flex items-center gap-2">
                              {stage.isSuccess ? (
                                <CheckCircle2 className="w-4 h-4 text-emerald-500" />
                              ) : (
                                <AlertCircle className="w-4 h-4 text-rose-500" />
                              )}
                              <span className="text-sm font-medium text-gray-900">{stage.stageName}</span>
                            </div>
                            <span className="text-xs text-gray-400">{formatDuration(stage.durationMs)}</span>
                          </div>
                          {stage.stepResults && stage.stepResults.length > 0 && (
                            <div className="space-y-1.5 mt-2 pl-6">
                              {stage.stepResults.map((step: StepResultDetail, sidx: number) => (
                                <div key={sidx} className="flex items-start gap-2 text-xs">
                                  {getStepIcon(step.status)}
                                  <div className="flex-1 min-w-0">
                                    <span className="font-medium text-gray-700">{step.stepName}</span>
                                    <span className="text-gray-400 ml-2">{formatDuration(step.durationMs)}</span>
                                    {step.errorMessage && (
                                      <p className="text-red-600 mt-0.5">{step.errorMessage}</p>
                                    )}
                                    {step.output && Object.keys(step.output).length > 0 && (
                                      <pre className="mt-1 text-[10px] bg-gray-50 rounded p-1.5 overflow-auto max-h-24 text-gray-600">
                                        {JSON.stringify(step.output, null, 2)}
                                      </pre>
                                    )}
                                  </div>
                                </div>
                              ))}
                            </div>
                          )}
                        </div>
                      ))}
                    </CardContent>
                  </Card>
                )}
              </div>
            ) : null}
          </div>
        </div>
      )}
    </div>
  )
}
