import { useState, useRef, useCallback } from 'react'
import { useNavigate } from 'react-router-dom'
import { Button } from '@/components/ui/Button'
import { Card, CardHeader, CardContent } from '@/components/ui/Card'
import { Input } from '@/components/ui/Input'
import { PageHeader } from '@/components/shared/PageHeader'
import { documentApi } from '@/services/api'
import { Upload, X, FileText, AlertCircle, CheckCircle } from 'lucide-react'

const ALLOWED_EXTENSIONS = ['.pdf', '.doc', '.docx', '.md', '.txt']
const ALLOWED_MIME_TYPES = [
  'application/pdf',
  'application/msword',
  'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
  'text/markdown',
  'text/plain',
]
const MAX_SIZE_MB = 50
const MAX_SIZE_BYTES = MAX_SIZE_MB * 1024 * 1024

function getExtension(filename: string): string {
  return filename.slice(filename.lastIndexOf('.')).toLowerCase()
}

function isValidFile(file: File): { valid: boolean; error?: string } {
  if (file.size > MAX_SIZE_BYTES) {
    return { valid: false, error: `文件大小超过 ${MAX_SIZE_MB}MB 限制` }
  }
  const ext = getExtension(file.name)
  if (!ALLOWED_EXTENSIONS.includes(ext)) {
    return { valid: false, error: `不支持的文件格式，仅支持 ${ALLOWED_EXTENSIONS.join('/')} ` }
  }
  if (!ALLOWED_MIME_TYPES.includes(file.type) && file.type !== '') {
    // Some systems may not set MIME type correctly, allow empty
    return { valid: false, error: `文件 MIME 类型不符合要求` }
  }
  return { valid: true }
}

export function UploadPage() {
  const navigate = useNavigate()
  const fileInputRef = useRef<HTMLInputElement>(null)
  const [dragOver, setDragOver] = useState(false)
  const [file, setFile] = useState<File | null>(null)
  const [error, setError] = useState('')
  const [uploading, setUploading] = useState(false)
  const [progress, setProgress] = useState(0)
  const [success, setSuccess] = useState(false)

  // Metadata
  const [title, setTitle] = useState('')
  const [departmentId, setDepartmentId] = useState('')
  const [visibility, setVisibility] = useState<'public' | 'department' | 'private'>('department')
  const [accessLevel, setAccessLevel] = useState<'readonly' | 'quotable'>('readonly')

  const handleFile = useCallback((selectedFile: File) => {
    const validation = isValidFile(selectedFile)
    if (!validation.valid) {
      setError(validation.error || '文件校验失败')
      return
    }
    setFile(selectedFile)
    setError('')
    // Auto-extract title from filename (remove extension)
    if (!title) {
      const nameWithoutExt = selectedFile.name.replace(/\.[^/.]+$/, '')
      setTitle(nameWithoutExt)
    }
  }, [title])

  const handleDrop = useCallback((e: React.DragEvent) => {
    e.preventDefault()
    setDragOver(false)
    const droppedFile = e.dataTransfer.files[0]
    if (droppedFile) {
      handleFile(droppedFile)
    }
  }, [handleFile])

  const handleDragOver = useCallback((e: React.DragEvent) => {
    e.preventDefault()
    setDragOver(true)
  }, [])

  const handleDragLeave = useCallback((e: React.DragEvent) => {
    e.preventDefault()
    setDragOver(false)
  }, [])

  const handleFileInput = (e: React.ChangeEvent<HTMLInputElement>) => {
    const selectedFile = e.target.files?.[0]
    if (selectedFile) {
      handleFile(selectedFile)
    }
  }

  const handleUpload = async () => {
    if (!file) return
    setUploading(true)
    setProgress(0)
    setError('')

    try {
      const metadata: Record<string, string> = {
        title: title || file.name.replace(/\.[^/.]+$/, ''),
        visibility,
        accessLevel,
      }
      if (departmentId) metadata.departmentId = departmentId
      const doc = await documentApi.upload(file, metadata)
      // Simulate progress since apiUpload doesn't support progress callbacks
      setProgress(100)
      setSuccess(true)
      // Navigate to detail page after short delay
      setTimeout(() => {
        navigate(`/documents/${doc.id}`)
      }, 1500)
    } catch (err) {
      setError(err instanceof Error ? err.message : '上传失败')
    } finally {
      setUploading(false)
    }
  }

  const handleCancel = () => {
    // In a real implementation, we would abort the XHR request
    // For now, just reset the state
    setUploading(false)
    setProgress(0)
    setError('上传已取消')
  }

  if (success) {
    return (
      <div className="p-4 md:p-8 max-w-2xl mx-auto">
        <Card className="text-center py-16">
          <div className="w-16 h-16 mx-auto mb-4 rounded-full bg-green-50 flex items-center justify-center">
            <CheckCircle className="w-8 h-8 text-green-600" />
          </div>
          <h2 className="text-xl font-semibold text-gray-900">上传成功</h2>
          <p className="mt-2 text-sm text-gray-500">文档已上传，正在跳转至详情页... </p>
        </Card>
      </div>
    )
  }

  return (
    <div className="p-4 md:p-8 max-w-2xl mx-auto">
      <PageHeader title="上传文档" description="上传知识库文档并填写元数据信息" />

      <Card>
        <CardHeader>
          <h3 className="text-base font-semibold text-gray-900">文件上传</h3>
        </CardHeader>
        <CardContent>
          {!file ? (
            <div
              onDrop={handleDrop}
              onDragOver={handleDragOver}
              onDragLeave={handleDragLeave}
              onClick={() => fileInputRef.current?.click()}
              className={`
                border-2 border-dashed rounded-xl p-8 text-center cursor-pointer transition-colors
                ${dragOver ? 'border-blue-500 bg-blue-50' : 'border-gray-300 hover:border-gray-400'}
              `}
            >
              <input
                ref={fileInputRef}
                type="file"
                className="hidden"
                accept={ALLOWED_EXTENSIONS.join(',')}
                onChange={handleFileInput}
              />
              <Upload className="w-10 h-10 mx-auto mb-3 text-gray-400" />
              <p className="text-sm font-medium text-gray-700">点击或拖拽文件至此处上传</p>
              <p className="text-xs text-gray-500 mt-1">
                支持 PDF、Word、Markdown、TXT，最大 {MAX_SIZE_MB}MB
              </p>
            </div>
          ) : (
            <div className="flex items-center gap-4 p-4 bg-gray-50 rounded-xl">
              <div className="w-10 h-10 rounded-lg bg-blue-50 flex items-center justify-center flex-shrink-0">
                <FileText className="w-5 h-5 text-blue-600" />
              </div>
              <div className="flex-1 min-w-0">
                <p className="text-sm font-medium text-gray-900 truncate">{file.name}</p>
                <p className="text-xs text-gray-500">{(file.size / 1024 / 1024).toFixed(2)} MB</p>
                {uploading && (
                  <div className="mt-2">
                    <div className="w-full bg-gray-200 rounded-full h-2">
                      <div
                        className="bg-blue-500 h-2 rounded-full transition-all duration-300"
                        style={{ width: `${progress}%` }}
                      />
                    </div>
                    <p className="text-xs text-gray-500 mt-1">{progress}%</p>
                  </div>
                )}
              </div>
              {!uploading && (
                <button
                  onClick={(e) => {
                    e.stopPropagation()
                    setFile(null)
                    setTitle('')
                    setProgress(0)
                  }}
                  className="flex-shrink-0 text-gray-400 hover:text-gray-600"
                >
                  <X className="w-5 h-5" />
                </button>
              )}
              {uploading && (
                <Button variant="ghost" size="sm" onClick={handleCancel}>
                  取消
                </Button>
              )}
            </div>
          )}

          {error && (
            <div className="mt-4 flex items-center gap-2 rounded-lg bg-red-50 px-4 py-3 text-sm text-red-600">
              <AlertCircle className="w-4 h-4 flex-shrink-0" />
              {error}
            </div>
          )}
        </CardContent>
      </Card>

      <Card className="mt-6">
        <CardHeader>
          <h3 className="text-base font-semibold text-gray-900">文档元数据</h3>
        </CardHeader>
        <CardContent className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              文档标题 *</label>
            <Input
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              placeholder="输入文档标题"
            />
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              所属部门</label>
            <select
              value={departmentId}
              onChange={(e) => setDepartmentId(e.target.value)}
              className="w-full px-4 py-2 rounded-lg border border-gray-300 bg-white text-gray-900 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent"
            >
              <option value="">请选择部门</option>
              <option value="dept-tech">技术部</option>
              <option value="dept-product">产品部</option>
              <option value="dept-ops">运营部</option>
              <option value="dept-hr">人力资源</option>
            </select>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                公开范围</label>
              <select
                value={visibility}
                onChange={(e) => setVisibility(e.target.value as 'public' | 'department' | 'private')}
                className="w-full px-4 py-2 rounded-lg border border-gray-300 bg-white text-gray-900 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent"
              >
                <option value="public">公开</option>
                <option value="department">部门内</option>
                <option value="private">私有</option>
              </select>
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                访问级别</label>
              <select
                value={accessLevel}
                onChange={(e) => setAccessLevel(e.target.value as 'readonly' | 'quotable')}
                className="w-full px-4 py-2 rounded-lg border border-gray-300 bg-white text-gray-900 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent"
              >
                <option value="readonly">只读</option>
                <option value="quotable">可引用</option>
              </select>
            </div>
          </div>
        </CardContent>
      </Card>

      <div className="flex items-center justify-end gap-3 mt-6">
        <Button variant="secondary" onClick={() => navigate('/documents')}>
          取消
        </Button>
        <Button
          onClick={handleUpload}
          disabled={!file || uploading}
          isLoading={uploading}
        >
          开始上传
        </Button>
      </div>
    </div>
  )
}
