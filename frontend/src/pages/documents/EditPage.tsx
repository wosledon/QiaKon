import { useState, useEffect, useCallback } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { Button } from '@/components/ui/Button'
import { Card, CardHeader, CardContent } from '@/components/ui/Card'
import { Input } from '@/components/ui/Input'
import { PageHeader } from '@/components/shared/PageHeader'
import { documentApi } from '@/services/api'
import type { Document } from '@/types'
import { ArrowLeft, Save, AlertCircle, Loader2, CheckCircle } from 'lucide-react'

export function EditPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const [doc, setDoc] = useState<Document | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')
  const [saved, setSaved] = useState(false)

  // Form state
  const [title, setTitle] = useState('')
  const [departmentId, setDepartmentId] = useState('')
  const [accessLevel, setAccessLevel] = useState<string>('department')

  const loadDoc = useCallback(async () => {
    if (!id) return
    setIsLoading(true)
    setError('')
    try {
      const data = await documentApi.get(id)
      setDoc(data)
      setTitle(data.title)
      setDepartmentId(data.departmentId)
      setAccessLevel(data.accessLevel || 'department')
    } catch (err) {
      setError(err instanceof Error ? err.message : '加载失败')
    } finally {
      setIsLoading(false)
    }
  }, [id])

  useEffect(() => {
    loadDoc()
  }, [loadDoc])

  const handleSave = async () => {
    if (!id) return
    if (!title.trim()) {
      setError('文档标题不能为空')
      return
    }
    setSaving(true)
    setError('')
    try {
      await documentApi.update(id, {
        title: title.trim(),
        accessLevel,
      })
      setSaved(true)
      setTimeout(() => {
        navigate(`/documents/${id}`)
      }, 1200)
    } catch (err) {
      setError(err instanceof Error ? err.message : '保存失败')
    } finally {
      setSaving(false)
    }
  }

  if (isLoading) {
    return (
      <div className="p-4 md:p-8 max-w-2xl mx-auto flex items-center justify-center py-24">
        <Loader2 className="w-8 h-8 animate-spin text-blue-500 mr-3" />
        <span className="text-gray-500">加载中...</span>
      </div>
    )
  }

  if (error && !doc) {
    return (
      <div className="p-4 md:p-8 max-w-2xl mx-auto">
        <div className="flex items-center gap-2 rounded-lg bg-red-50 px-4 py-3 text-sm text-red-600">
          <AlertCircle className="w-4 h-4" />
          {error}
        </div>
        <Button variant="secondary" className="mt-4" onClick={() => navigate('/documents')}>
          <ArrowLeft className="w-4 h-4 mr-2" />
          返回列表
        </Button>
      </div>
    )
  }

  if (saved) {
    return (
      <div className="p-4 md:p-8 max-w-2xl mx-auto">
        <Card className="text-center py-16">
          <div className="w-16 h-16 mx-auto mb-4 rounded-full bg-green-50 flex items-center justify-center">
            <CheckCircle className="w-8 h-8 text-green-600" />
          </div>
          <h2 className="text-xl font-semibold text-gray-900">保存成功</h2>
          <p className="mt-2 text-sm text-gray-500">文档元数据已更新，正在跳转...</p>
        </Card>
      </div>
    )
  }

  return (
    <div className="p-4 md:p-8 max-w-2xl mx-auto">
      <PageHeader title="编辑文档" description={`编辑「${doc?.title}」的元数据信息`}>
        <Button variant="secondary" size="sm" onClick={() => navigate(`/documents/${id}`)}>
          <ArrowLeft className="w-4 h-4 mr-1" />
          返回
        </Button>
      </PageHeader>

      {error && (
        <div className="mb-4 flex items-center gap-2 rounded-lg bg-red-50 px-4 py-3 text-sm text-red-600">
          <AlertCircle className="w-4 h-4" />
          {error}
        </div>
      )}

      <Card>
        <CardHeader>
          <h3 className="text-base font-semibold text-gray-900">文档元数据</h3>
        </CardHeader>
        <CardContent className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              文档标题 <span className="text-red-500">*</span>
            </label>
            <Input
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              placeholder="输入文档标题"
            />
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              所属部门
            </label>
            <select
              value={departmentId}
              onChange={(e) => setDepartmentId(e.target.value)}
              disabled
              className="w-full px-4 py-2 rounded-lg border border-gray-300 bg-gray-50 text-gray-500 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent"
            >
              <option value="">请选择部门</option>
              <option value="11111111-1111-1111-1111-111111111111">研发部</option>
              <option value="22222222-2222-2222-2222-222222222222">销售部</option>
              <option value="33333333-3333-3333-3333-333333333333">人力资源部</option>
              <option value="44444444-4444-4444-4444-444444444444">行政部</option>
            </select>
            <p className="text-xs text-gray-400 mt-1">部门信息暂不支持修改</p>
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              访问级别
            </label>
            <select
              value={accessLevel}
              onChange={(e) => setAccessLevel(e.target.value)}
              className="w-full px-4 py-2 rounded-lg border border-gray-300 bg-white text-gray-900 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent"
            >
              <option value="public">公开</option>
              <option value="department">部门内</option>
              <option value="restricted">受限</option>
              <option value="confidential">机密</option>
            </select>
          </div>
        </CardContent>
      </Card>

      <div className="flex items-center justify-end gap-3 mt-6">
        <Button variant="secondary" onClick={() => navigate(`/documents/${id}`)}>
          取消
        </Button>
        <Button onClick={handleSave} isLoading={saving}>
          <Save className="w-4 h-4 mr-2" />
          保存
        </Button>
      </div>
    </div>
  )
}
