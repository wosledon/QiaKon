import { useState, useRef, useCallback } from 'react'
import { useNavigate } from 'react-router-dom'
import { Button } from '@/components/ui/Button'
import { Card, CardHeader, CardContent, CardFooter } from '@/components/ui/Card'
import { Input } from '@/components/ui/Input'
import { Select } from '@/components/ui/Select'
import { PageHeader } from '@/components/shared/PageHeader'
import { documentApi } from '@/services/api'
import {
  Upload,
  X,
  FileText,
  AlertCircle,
  CheckCircle,
  Info,
  FileCheck,
} from 'lucide-react'

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

function formatFileSize(bytes: number): string {
  if (bytes === 0) return '0 B'
  const k = 1024
  const sizes = ['B', 'KB', 'MB', 'GB']
  const i = Math.floor(Math.log(bytes) / Math.log(k))
  return `${parseFloat((bytes / Math.pow(k, i)).toFixed(2))} ${sizes[i]}`
}

function isValidFile(file: File): { valid: boolean; error?: string } {
  if (file.size > MAX_SIZE_BYTES) {
    return { valid: false, error: `文件大小超过 ${MAX_SIZE_MB}MB 限制` }
  }
  const ext = getExtension(file.name)
  if (!ALLOWED_EXTENSIONS.includes(ext)) {
    return {
      valid: false,
      error: `不支持的文件格式，仅支持 ${ALLOWED_EXTENSIONS.join('/')} `,
    }
  }
  if (!ALLOWED_MIME_TYPES.includes(file.type) && file.type !== '') {
    // Some systems may not set MIME type correctly, allow empty
    return { valid: false, error: `文件 MIME 类型不符合要求` }
  }
  return { valid: true }
}

const DEPARTMENT_OPTIONS = [
  { value: 'dept-tech', label: '技术部' },
  { value: 'dept-product', label: '产品部' },
  { value: 'dept-ops', label: '运营部' },
  { value: 'dept-hr', label: '人力资源' },
]

const VISIBILITY_OPTIONS = [
  { value: 'public', label: '公开' },
  { value: 'department', label: '部门内' },
  { value: 'private', label: '私有' },
]

const ACCESS_LEVEL_OPTIONS = [
  { value: 'readonly', label: '只读' },
  { value: 'quotable', label: '可引用' },
]

const FILE_TYPE_TAGS = ['PDF', 'DOCX', 'MD', 'TXT']

export function UploadPage() {
  const navigate = useNavigate()
  const fileInputRef = useRef<HTMLInputElement>(null)
  const [dragOver, setDragOver] = useState(false)
  const [file, setFile] = useState<File | null>(null)
  const [error, setError] = useState('')
  const [uploading, setUploading] = useState(false)
  const [progress, setProgress] = useState(0)
  const [success, setSuccess] = useState(false)
  const [uploadedDocId, setUploadedDocId] = useState<string | null>(null)

  // Metadata
  const [title, setTitle] = useState('')
  const [departmentId, setDepartmentId] = useState('')
  const [visibility, setVisibility] = useState<'public' | 'department' | 'private'>('department')
  const [accessLevel, setAccessLevel] = useState<'readonly' | 'quotable'>('readonly')

  const handleFile = useCallback(
    (selectedFile: File) => {
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
    },
    [title]
  )

  const handleDrop = useCallback(
    (e: React.DragEvent) => {
      e.preventDefault()
      setDragOver(false)
      const droppedFile = e.dataTransfer.files[0]
      if (droppedFile) {
        handleFile(droppedFile)
      }
    },
    [handleFile]
  )

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
      setUploadedDocId(doc.id)
      setProgress(100)
      setSuccess(true)
      setTimeout(() => {
        navigate(`/documents/${doc.id}`)
      }, 2000)
    } catch (err) {
      setError(err instanceof Error ? err.message : '上传失败')
    } finally {
      setUploading(false)
    }
  }

  const handleCancel = () => {
    setUploading(false)
    setProgress(0)
    setError('上传已取消')
  }

  const clearFile = (e: React.MouseEvent) => {
    e.stopPropagation()
    setFile(null)
    setTitle('')
    setProgress(0)
    setError('')
  }

  if (success) {
    return (
      <div className="mx-auto w-full max-w-7xl p-4 md:p-8">
        <div className="max-w-lg mx-auto">
          <Card className="text-center py-16">
            <div className="w-20 h-20 mx-auto mb-6 rounded-full bg-green-50 flex items-center justify-center">
              <CheckCircle className="w-10 h-10 text-green-600" />
            </div>
            <h2 className="text-2xl font-bold text-gray-900">上传成功</h2>
            <p className="mt-3 text-sm text-gray-500">
              文档已上传，正在跳转至详情页...
            </p>
            {uploadedDocId && (
              <Button
                className="mt-8"
                variant="secondary"
                onClick={() => navigate(`/documents/${uploadedDocId}`)}
              >
                立即查看
              </Button>
            )}
          </Card>
        </div>
      </div>
    )
  }

  return (
    <div className="mx-auto w-full max-w-7xl p-4 md:p-6 lg:p-8">
      <PageHeader
        title="上传文档"
        description="上传知识库文档并填写元数据信息"
      />

      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 lg:gap-8">
        {/* Left column: upload zone + guidelines */}
        <div className="lg:col-span-5 space-y-6">
          <Card>
            <CardHeader>
              <h3 className="text-base font-semibold text-gray-900">
                选择文件
              </h3>
            </CardHeader>
            <CardContent>
              {!file ? (
                <div
                  onDrop={handleDrop}
                  onDragOver={handleDragOver}
                  onDragLeave={handleDragLeave}
                  onClick={() => fileInputRef.current?.click()}
                  className={`
                    border-2 border-dashed rounded-xl py-12 px-6 text-center cursor-pointer transition-all duration-200
                    ${dragOver ? 'border-blue-500 bg-blue-50/80 scale-[1.01]' : 'border-gray-300 hover:border-gray-400 hover:bg-gray-50/50'}
                  `}
                >
                  <input
                    ref={fileInputRef}
                    type="file"
                    className="hidden"
                    accept={ALLOWED_EXTENSIONS.join(',')}
                    onChange={handleFileInput}
                  />
                  <div
                    className={`
                      w-14 h-14 mx-auto mb-4 rounded-xl flex items-center justify-center transition-colors
                      ${dragOver ? 'bg-blue-100' : 'bg-gray-100'}
                    `}
                  >
                    <Upload
                      className={`w-7 h-7 ${dragOver ? 'text-blue-600' : 'text-gray-400'}`}
                    />
                  </div>
                  <p className="text-sm font-semibold text-gray-900">
                    {dragOver
                      ? '释放以上传文件'
                      : '点击或拖拽文件至此处'}
                  </p>
                  <p className="text-xs text-gray-500 mt-2">
                    支持 PDF、Word、Markdown、TXT，最大 {MAX_SIZE_MB}MB
                  </p>

                  <div className="flex flex-wrap justify-center gap-2 mt-4">
                    {FILE_TYPE_TAGS.map((type) => (
                      <span
                        key={type}
                        className="inline-flex items-center px-2 py-1 rounded-md bg-gray-100 text-xs font-medium text-gray-600"
                      >
                        {type}
                      </span>
                    ))}
                  </div>
                </div>
              ) : (
                <div className="flex items-start gap-4 p-5 bg-gray-50 rounded-xl border border-gray-100">
                  <div className="w-12 h-12 rounded-xl bg-blue-50 flex items-center justify-center flex-shrink-0">
                    <FileText className="w-6 h-6 text-blue-600" />
                  </div>
                  <div className="flex-1 min-w-0">
                    <p className="text-sm font-semibold text-gray-900 truncate">
                      {file.name}
                    </p>
                    <p className="text-xs text-gray-500 mt-0.5">
                      {formatFileSize(file.size)} ·{' '}
                      {getExtension(file.name).toUpperCase().replace('.', '')}
                    </p>

                    {uploading && (
                      <div className="mt-3">
                        <div className="flex items-center justify-between text-xs text-gray-500 mb-1">
                          <span>上传中...</span>
                          <span>{progress}%</span>
                        </div>
                        <div className="w-full bg-gray-200 rounded-full h-2">
                          <div
                            className="bg-blue-500 h-2 rounded-full transition-all duration-300"
                            style={{ width: `${progress}%` }}
                          />
                        </div>
                      </div>
                    )}

                    {!uploading && !error && (
                      <div className="mt-2 flex items-center gap-1.5 text-xs text-green-600">
                        <FileCheck className="w-3.5 h-3.5" />
                        <span>已通过文件检查</span>
                      </div>
                    )}
                  </div>

                  {!uploading && (
                    <button
                      onClick={clearFile}
                      className="flex-shrink-0 p-1 rounded-md text-gray-400 hover:text-red-500 hover:bg-red-50 transition-colors"
                      title="移除文件"
                    >
                      <X className="w-5 h-5" />
                    </button>
                  )}
                  {uploading && (
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={handleCancel}
                    >
                      取消
                    </Button>
                  )}
                </div>
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <h3 className="text-base font-semibold text-gray-900">
                上传须知
              </h3>
            </CardHeader>
            <CardContent className="space-y-3">
              <div className="flex items-start gap-3">
                <Info className="w-4 h-4 text-blue-500 mt-0.5 flex-shrink-0" />
                <div>
                  <p className="text-sm font-medium text-gray-700">
                    文件格式
                  </p>
                  <p className="text-xs text-gray-500">
                    PDF、Word（.doc/.docx）、Markdown、纯文本
                  </p>
                </div>
              </div>
              <div className="flex items-start gap-3">
                <Info className="w-4 h-4 text-blue-500 mt-0.5 flex-shrink-0" />
                <div>
                  <p className="text-sm font-medium text-gray-700">
                    大小限制
                  </p>
                  <p className="text-xs text-gray-500">
                    单个文件不超过 {MAX_SIZE_MB}MB
                  </p>
                </div>
              </div>
              <div className="flex items-start gap-3">
                <Info className="w-4 h-4 text-blue-500 mt-0.5 flex-shrink-0" />
                <div>
                  <p className="text-sm font-medium text-gray-700">
                    元数据
                  </p>
                  <p className="text-xs text-gray-500">
                    上传前请确认标题、可见范围和访问级别
                  </p>
                </div>
              </div>
            </CardContent>
          </Card>
        </div>

        {/* Right column: metadata + actions */}
        <div className="lg:col-span-7">
          <Card className="h-full flex flex-col">
            <CardHeader>
              <h3 className="text-base font-semibold text-gray-900">
                文档元数据
              </h3>
              <p className="text-xs text-gray-500 mt-1">
                填写文档基本信息以便分类和检索
              </p>
            </CardHeader>
            <CardContent className="space-y-5 flex-1">
              <Input
                label="文档标题"
                required
                value={title}
                onChange={(e) => setTitle(e.target.value)}
                placeholder="输入文档标题"
              />

              <Select
                label="所属部门"
                placeholder="请选择部门"
                value={departmentId}
                onChange={(e) => setDepartmentId(e.target.value)}
                options={DEPARTMENT_OPTIONS}
              />

              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <Select
                  label="公开范围"
                  value={visibility}
                  onChange={(e) =>
                    setVisibility(
                      e.target.value as 'public' | 'department' | 'private'
                    )
                  }
                  options={VISIBILITY_OPTIONS}
                />
                <Select
                  label="访问级别"
                  value={accessLevel}
                  onChange={(e) =>
                    setAccessLevel(e.target.value as 'readonly' | 'quotable')
                  }
                  options={ACCESS_LEVEL_OPTIONS}
                />
              </div>
            </CardContent>

            <CardFooter className="bg-gray-50/50">
              <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4 w-full">
                <div className="text-xs text-gray-500">
                  {file ? (
                    <span className="flex items-center gap-1.5">
                      <FileCheck className="w-3.5 h-3.5 text-green-500" />
                      已选择 {file.name}（{formatFileSize(file.size)}）
                    </span>
                  ) : (
                    <span className="flex items-center gap-1.5">
                      <AlertCircle className="w-3.5 h-3.5 text-amber-500" />
                      请先选择要上传的文件
                    </span>
                  )}
                </div>
                <div className="flex items-center justify-end gap-3">
                  <Button
                    variant="secondary"
                    onClick={() => navigate('/documents')}
                  >
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
            </CardFooter>
          </Card>
        </div>
      </div>

      {error && (
        <div className="mt-6 max-w-2xl">
          <div className="flex items-center gap-2 rounded-lg bg-red-50 border border-red-100 px-4 py-3 text-sm text-red-600">
            <AlertCircle className="w-4 h-4 flex-shrink-0" />
            {error}
          </div>
        </div>
      )}
    </div>
  )
}
