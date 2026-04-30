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
  { value: '11111111-1111-1111-1111-111111111111', label: '研发部' },
  { value: '22222222-2222-2222-2222-222222222222', label: '销售部' },
  { value: '33333333-3333-3333-3333-333333333333', label: '人力资源部' },
  { value: '44444444-4444-4444-4444-444444444444', label: '行政部' },
]

const ACCESS_LEVEL_OPTIONS = [
  { value: 'Public', label: '公开' },
  { value: 'Department', label: '部门内' },
  { value: 'Restricted', label: '受限' },
  { value: 'Confidential', label: '机密' },
]

const CHUNKING_STRATEGY_OPTIONS = [
  { value: 'Auto', label: '自动（优先使用默认 MoE 分块模型）' },
  { value: 'MoE', label: 'MoE 语义分块' },
  { value: 'Character', label: '字符分块' },
]

const FILE_TYPE_TAGS = ['PDF', 'DOCX', 'MD', 'TXT']

type UploadStatus = 'ready' | 'uploading' | 'success' | 'error'

interface UploadItem {
  id: string
  file: File
  status: UploadStatus
  error?: string
  documentId?: string
}

export function UploadPage() {
  const navigate = useNavigate()
  const fileInputRef = useRef<HTMLInputElement>(null)
  const abortRef = useRef<AbortController | null>(null)
  const [dragOver, setDragOver] = useState(false)
  const [files, setFiles] = useState<UploadItem[]>([])
  const [error, setError] = useState('')
  const [uploading, setUploading] = useState(false)
  const [progress, setProgress] = useState(0)
  const [success, setSuccess] = useState(false)
  const [uploadedDocIds, setUploadedDocIds] = useState<string[]>([])

  // Metadata
  const [title, setTitle] = useState('')
  const [description, setDescription] = useState('')
  const [departmentId, setDepartmentId] = useState('')
  const [accessLevel, setAccessLevel] = useState<'Public' | 'Department' | 'Restricted' | 'Confidential'>('Department')
  const [chunkingStrategy, setChunkingStrategy] = useState<'Auto' | 'MoE' | 'Character'>('Auto')

  const isMultiUpload = files.length > 1
  const uploadedCount = uploadedDocIds.length
  const filesPendingUpload = files.filter((item) => item.status !== 'success')

  const handleFiles = useCallback((selectedFiles: File[]) => {
    if (selectedFiles.length === 0) {
      return
    }

    let nextFilesSnapshot: UploadItem[] = []
    const selectionErrors: string[] = []

    setFiles((prev) => {
      const next = [...prev]
      const existingKeys = new Set(prev.map((item) => `${item.file.name}-${item.file.size}-${item.file.lastModified}`))

      for (const selectedFile of selectedFiles) {
        const validation = isValidFile(selectedFile)
        if (!validation.valid) {
          selectionErrors.push(`${selectedFile.name}：${validation.error || '文件校验失败'}`)
          continue
        }

        const fileKey = `${selectedFile.name}-${selectedFile.size}-${selectedFile.lastModified}`
        if (existingKeys.has(fileKey)) {
          selectionErrors.push(`${selectedFile.name}：该文件已在待上传列表中`)
          continue
        }

        existingKeys.add(fileKey)
        next.push({
          id: crypto.randomUUID(),
          file: selectedFile,
          status: 'ready',
        })
      }

      nextFilesSnapshot = next
      return next
    })

    if (!title && nextFilesSnapshot.length === 1) {
      setTitle(nextFilesSnapshot[0].file.name.replace(/\.[^/.]+$/, ''))
    }

    if (selectionErrors.length > 0) {
      setError(selectionErrors.join('；'))
      return
    }

    setError('')
  }, [title])

  const handleDrop = useCallback(
    (e: React.DragEvent) => {
      e.preventDefault()
      setDragOver(false)
      const droppedFiles = Array.from(e.dataTransfer.files)
      if (droppedFiles.length > 0) {
        handleFiles(droppedFiles)
      }
    },
    [handleFiles]
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
    const selectedFiles = Array.from(e.target.files ?? [])
    if (selectedFiles.length > 0) {
      handleFiles(selectedFiles)
    }

    e.target.value = ''
  }

  const handleUpload = async () => {
    if (filesPendingUpload.length === 0) return

    setUploading(true)
    setProgress(0)
    setError('')
    setSuccess(false)

    abortRef.current?.abort()
    const abortController = new AbortController()
    abortRef.current = abortController

    const succeededDocIds: string[] = []
    const uploadErrors: string[] = []

    setFiles((prev) => prev.map((item) => (
      item.status === 'error'
        ? { ...item, status: 'ready', error: undefined }
        : item
    )))

    try {
      for (const [index, item] of filesPendingUpload.entries()) {
        setFiles((prev) => prev.map((current) => (
          current.id === item.id
            ? { ...current, status: 'uploading', error: undefined }
            : current
        )))

        try {
          const metadata: Record<string, string> = {
            accessLevel: accessLevel as string,
            chunkingStrategy,
          }

          if (!isMultiUpload) {
            metadata.title = title || item.file.name.replace(/\.[^/.]+$/, '')
          }

          if (departmentId) metadata.departmentId = departmentId
          if (description) metadata.description = description

          const doc = await documentApi.upload(item.file, metadata, { signal: abortController.signal })
          succeededDocIds.push(doc.id)

          setFiles((prev) => prev.map((current) => (
            current.id === item.id
              ? { ...current, status: 'success', documentId: doc.id, error: undefined }
              : current
          )))
        } catch (err) {
          if (err instanceof DOMException && err.name === 'AbortError') {
            throw err
          }

          const message = err instanceof Error ? err.message : '上传失败'
          uploadErrors.push(`${item.file.name}：${message}`)

          setFiles((prev) => prev.map((current) => (
            current.id === item.id
              ? { ...current, status: 'error', error: message }
              : current
          )))
        } finally {
          setProgress(Math.round(((index + 1) / filesPendingUpload.length) * 100))
        }
      }

      if (uploadErrors.length === 0) {
        const allUploadedIds = [...uploadedDocIds, ...succeededDocIds]
        setUploadedDocIds(allUploadedIds)
        setSuccess(true)
        setTimeout(() => {
          if (allUploadedIds.length === 1) {
            navigate(`/documents/${allUploadedIds[0]}`)
            return
          }

          navigate('/documents')
        }, 2000)
      } else {
        if (succeededDocIds.length > 0) {
          setUploadedDocIds((prev) => [...prev, ...succeededDocIds])
        }

        const successMessage = succeededDocIds.length > 0
          ? `已成功上传 ${succeededDocIds.length} 个文件，`
          : ''
        setError(`${successMessage}${uploadErrors.length} 个文件上传失败：${uploadErrors.join('；')}`)
      }
    } catch (err) {
      if (err instanceof DOMException && err.name === 'AbortError') {
        setError('上传已取消')
      } else {
        setError(err instanceof Error ? err.message : '上传失败')
      }
    } finally {
      setUploading(false)
      abortRef.current = null
    }
  }

  const handleCancel = () => {
    abortRef.current?.abort()
    setProgress(0)
  }

  const removeFile = (fileId: string) => (e: React.MouseEvent) => {
    e.stopPropagation()
    let nextCount = 0

    setFiles((prev) => {
      const next = prev.filter((item) => item.id !== fileId)
      nextCount = next.length
      return next
    })

    if (nextCount === 0) {
      setTitle('')
      setProgress(0)
      setError('')
      setUploadedDocIds([])
      return
    }

    if (nextCount === 1 && !title) {
      const remaining = files.find((item) => item.id !== fileId)
      if (remaining) {
        setTitle(remaining.file.name.replace(/\.[^/.]+$/, ''))
      }
    }
  }

  if (success) {
    return (
      <div className="mx-auto w-full max-w-7xl p-4 md:p-8">
        <div className="max-w-lg mx-auto">
          <Card className="text-center py-16">
            <div className="w-20 h-20 mx-auto mb-6 rounded-full bg-green-50 flex items-center justify-center">
              <CheckCircle className="w-10 h-10 text-green-600" />
            </div>
            <h2 className="text-2xl font-bold text-gray-900">
              {uploadedCount > 1 ? `成功上传 ${uploadedCount} 个文档` : '上传成功'}
            </h2>
            <p className="mt-3 text-sm text-gray-500">
              {uploadedCount > 1 ? '文档已批量上传，正在跳转至文档列表...' : '文档已上传，正在跳转至详情页...'}
            </p>
            {uploadedCount > 0 && (
              <Button
                className="mt-8"
                variant="secondary"
                onClick={() => navigate(uploadedCount > 1 ? '/documents' : `/documents/${uploadedDocIds[0]}`)}
              >
                {uploadedCount > 1 ? '查看文档列表' : '立即查看'}
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
              {files.length === 0 ? (
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
                    multiple
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
                      : '点击或拖拽一个或多个文件至此处'}
                  </p>
                  <p className="text-xs text-gray-500 mt-2">
                    支持 PDF、Word、Markdown、TXT，可多选上传，单个文件最大 {MAX_SIZE_MB}MB
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
                <div className="space-y-3">
                  <div className="flex items-center justify-between rounded-xl border border-blue-100 bg-blue-50/70 px-4 py-3">
                    <div>
                      <p className="text-sm font-semibold text-gray-900">已选择 {files.length} 个文件</p>
                      <p className="text-xs text-gray-500 mt-1">
                        {isMultiUpload ? '多文件上传时将自动使用文件名作为文档标题。' : '可继续添加文件，统一使用右侧元数据配置。'}
                      </p>
                    </div>

                    <div className="flex items-center gap-2">
                      {!uploading && (
                        <Button variant="secondary" size="sm" onClick={() => fileInputRef.current?.click()}>
                          继续添加
                        </Button>
                      )}
                      {uploading && (
                        <Button variant="ghost" size="sm" onClick={handleCancel}>
                          取消
                        </Button>
                      )}
                    </div>
                  </div>

                  {uploading && (
                    <div className="rounded-xl border border-gray-100 bg-gray-50 px-4 py-3">
                      <div className="flex items-center justify-between text-xs text-gray-500 mb-2">
                        <span>批量上传中...</span>
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

                  <div className="space-y-3 max-h-[420px] overflow-y-auto pr-1">
                    {files.map((item) => (
                      <div key={item.id} className="flex items-start gap-4 p-4 bg-gray-50 rounded-xl border border-gray-100">
                        <div className="w-12 h-12 rounded-xl bg-blue-50 flex items-center justify-center flex-shrink-0">
                          <FileText className="w-6 h-6 text-blue-600" />
                        </div>

                        <div className="flex-1 min-w-0">
                          <div className="flex flex-wrap items-center gap-2">
                            <p className="text-sm font-semibold text-gray-900 truncate max-w-full">
                              {item.file.name}
                            </p>
                            <span className={[
                              'inline-flex items-center rounded-full px-2 py-0.5 text-[10px] font-medium',
                              item.status === 'success'
                                ? 'bg-green-100 text-green-700'
                                : item.status === 'error'
                                ? 'bg-red-100 text-red-700'
                                : item.status === 'uploading'
                                ? 'bg-blue-100 text-blue-700'
                                : 'bg-gray-200 text-gray-600',
                            ].join(' ')}>
                              {item.status === 'success'
                                ? '已完成'
                                : item.status === 'error'
                                ? '失败'
                                : item.status === 'uploading'
                                ? '上传中'
                                : '待上传'}
                            </span>
                          </div>

                          <p className="text-xs text-gray-500 mt-0.5">
                            {formatFileSize(item.file.size)} · {getExtension(item.file.name).toUpperCase().replace('.', '')}
                          </p>

                          {item.status === 'ready' && (
                            <div className="mt-2 flex items-center gap-1.5 text-xs text-green-600">
                              <FileCheck className="w-3.5 h-3.5" />
                              <span>已通过文件检查</span>
                            </div>
                          )}

                          {item.error && (
                            <p className="mt-2 text-xs text-red-600">{item.error}</p>
                          )}
                        </div>

                        {!uploading && (
                          <button
                            onClick={removeFile(item.id)}
                            className="flex-shrink-0 p-1 rounded-md text-gray-400 hover:text-red-500 hover:bg-red-50 transition-colors"
                            title="移除文件"
                          >
                            <X className="w-5 h-5" />
                          </button>
                        )}
                      </div>
                    ))}
                  </div>

                  <input
                    ref={fileInputRef}
                    type="file"
                    className="hidden"
                    multiple
                    accept={ALLOWED_EXTENSIONS.join(',')}
                    onChange={handleFileInput}
                  />
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
                    PDF、Word（.doc/.docx）、Markdown、纯文本，支持单次多选上传
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
                placeholder={isMultiUpload ? '多文件上传时自动使用文件名作为标题' : '输入文档标题'}
                disabled={isMultiUpload}
              />

              {isMultiUpload && (
                <p className="-mt-3 text-xs text-gray-500">
                  当前为多文件上传，系统会为每个文件自动使用“文件名（去掉扩展名）”作为文档标题。
                </p>
              )}

              <Select
                label="所属部门"
                placeholder="请选择部门"
                value={departmentId}
                onChange={(e) => setDepartmentId(e.target.value)}
                options={DEPARTMENT_OPTIONS}
              />

              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  描述
                </label>
                <textarea
                  value={description}
                  onChange={(e) => setDescription(e.target.value)}
                  placeholder="输入文档描述（可选）"
                  rows={3}
                  className="w-full px-3 py-2 rounded-lg border border-gray-300 bg-white text-gray-900 placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent text-sm"
                />
              </div>

              <Select
                label="访问级别"
                value={accessLevel}
                onChange={(e) =>
                  setAccessLevel(
                    e.target.value as 'Public' | 'Department' | 'Restricted' | 'Confidential'
                  )
                }
                options={ACCESS_LEVEL_OPTIONS}
              />

              <Select
                label="分块策略"
                value={chunkingStrategy}
                onChange={(e) => setChunkingStrategy(e.target.value as 'Auto' | 'MoE' | 'Character')}
                options={CHUNKING_STRATEGY_OPTIONS}
              />

              <p className="-mt-3 text-xs text-gray-500">
                选择“自动”时，后端会优先使用你在大模型管理中设置的默认分块模型来触发 MoE 分块。
              </p>
            </CardContent>

            <CardFooter className="bg-gray-50/50">
              <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4 w-full">
                <div className="text-xs text-gray-500">
                  {files.length > 0 ? (
                    <span className="flex items-center gap-1.5">
                      <FileCheck className="w-3.5 h-3.5 text-green-500" />
                      已选择 {files.length} 个文件，待上传 {filesPendingUpload.length} 个
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
                    disabled={filesPendingUpload.length === 0 || uploading}
                    isLoading={uploading}
                  >
                    {isMultiUpload ? '开始批量上传' : '开始上传'}
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
