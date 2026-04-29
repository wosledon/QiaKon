import { useState, useEffect, useCallback } from 'react'
import { useNavigate } from 'react-router-dom'
import { Button } from '@/components/ui/Button'
import { Dialog } from '@/components/ui/Dialog'
import { Input } from '@/components/ui/Input'
import { Card, CardContent } from '@/components/ui/Card'
import { PageHeader } from '@/components/shared/PageHeader'
import { adminApi } from '@/services/api'
import { isAdminRole, useAuth } from '@/stores/authStore'
import type { PermissionMatrix, Role } from '@/types'
import {
  Plus,
  Edit2,
  Trash2,
  Shield,
  Users,
  Save,
  CheckSquare,
  Square,
} from 'lucide-react'

function createEmptyPermissions(): PermissionMatrix {
  return {
    canReadPublicDocuments: false,
    canWritePublicDocuments: false,
    canDeletePublicDocuments: false,
    canReadDepartmentDocuments: false,
    canWriteDepartmentDocuments: false,
    canDeleteDepartmentDocuments: false,
    canReadAllDocuments: false,
    canWriteAllDocuments: false,
    canDeleteAllDocuments: false,
    canManageUsers: false,
    canManageRoles: false,
    canManageDepartments: false,
    canViewAuditLogs: false,
    canManageSystemConfig: false,
  }
}

interface MatrixRow {
  category: string
  resource: string
  perms: { key: keyof PermissionMatrix; label: string }[]
}

const matrixRows: MatrixRow[] = [
  {
    category: '文档',
    resource: '公开文档',
    perms: [
      { key: 'canReadPublicDocuments', label: '查看' },
      { key: 'canWritePublicDocuments', label: '新建/编辑' },
      { key: 'canDeletePublicDocuments', label: '删除' },
    ],
  },
  {
    category: '文档',
    resource: '本部门文档',
    perms: [
      { key: 'canReadDepartmentDocuments', label: '查看' },
      { key: 'canWriteDepartmentDocuments', label: '新建/编辑' },
      { key: 'canDeleteDepartmentDocuments', label: '删除' },
    ],
  },
  {
    category: '文档',
    resource: '所有文档',
    perms: [
      { key: 'canReadAllDocuments', label: '查看' },
      { key: 'canWriteAllDocuments', label: '新建/编辑' },
      { key: 'canDeleteAllDocuments', label: '删除' },
    ],
  },
  {
    category: '系统',
    resource: '用户管理',
    perms: [{ key: 'canManageUsers', label: '管理' }],
  },
  {
    category: '系统',
    resource: '角色管理',
    perms: [{ key: 'canManageRoles', label: '管理' }],
  },
  {
    category: '系统',
    resource: '部门管理',
    perms: [{ key: 'canManageDepartments', label: '管理' }],
  },
  {
    category: '系统',
    resource: '审计日志',
    perms: [{ key: 'canViewAuditLogs', label: '查看' }],
  },
  {
    category: '系统',
    resource: '系统配置',
    perms: [{ key: 'canManageSystemConfig', label: '管理' }],
  },
]

export function AdminRolesPage() {
  const navigate = useNavigate()
  const { user: currentUser } = useAuth()
  const isAdmin = isAdminRole(currentUser?.role)

  const [roles, setRoles] = useState<Role[]>([])
  const [isLoading, setIsLoading] = useState(false)
  const [editingRole, setEditingRole] = useState<Role | null>(null)
  const [dialogOpen, setDialogOpen] = useState(false)
  const [dialogMode, setDialogMode] = useState<'create' | 'edit'>('create')
  const [confirmDelete, setConfirmDelete] = useState<Role | null>(null)
  const [matrixRole, setMatrixRole] = useState<Role | null>(null)
  const [matrix, setMatrix] = useState<PermissionMatrix>(createEmptyPermissions())
  const [savingMatrix, setSavingMatrix] = useState(false)

  const [form, setForm] = useState({ name: '', description: '' })

  useEffect(() => {
    if (!isAdmin) {
      navigate('/', { replace: true })
    }
  }, [isAdmin, navigate])

  const loadRoles = useCallback(async () => {
    setIsLoading(true)
    try {
      const data = await adminApi.roles.list()
      setRoles(data)
    } catch (err) {
      console.error(err)
    } finally {
      setIsLoading(false)
    }
  }, [])

  useEffect(() => {
    if (isAdmin) loadRoles()
  }, [loadRoles, isAdmin])

  const openCreate = () => {
    setForm({ name: '', description: '' })
    setDialogMode('create')
    setDialogOpen(true)
  }

  const openEdit = (role: Role) => {
    setForm({ name: role.name, description: role.description || '' })
    setEditingRole(role)
    setDialogMode('edit')
    setDialogOpen(true)
  }

  const openMatrix = (role: Role) => {
    setMatrixRole(role)
    setMatrix(role.permissions || createEmptyPermissions())
  }

  const handleSave = async () => {
    try {
      if (dialogMode === 'create') {
        await adminApi.roles.create({
          name: form.name,
          description: form.description,
          permissions: createEmptyPermissions(),
        })
      } else if (editingRole) {
        await adminApi.roles.update(editingRole.id, {
          name: form.name,
          description: form.description,
        })
      }
      setDialogOpen(false)
      setEditingRole(null)
      await loadRoles()
    } catch (err) {
      alert(err instanceof Error ? err.message : '操作失败')
    }
  }

  const handleDelete = async () => {
    if (!confirmDelete) return
    try {
      await adminApi.roles.delete(confirmDelete.id)
      setConfirmDelete(null)
      await loadRoles()
    } catch (err) {
      alert(err instanceof Error ? err.message : '删除失败')
    }
  }

  const handleSaveMatrix = async () => {
    if (!matrixRole) return
    setSavingMatrix(true)
    try {
      await adminApi.roles.updatePermissions(matrixRole.id, matrix)
      setMatrixRole(null)
      await loadRoles()
    } catch (err) {
      alert(err instanceof Error ? err.message : '保存失败')
    } finally {
      setSavingMatrix(false)
    }
  }

  const toggleCell = (permKey: keyof PermissionMatrix) => {
    setMatrix((prev) => ({ ...prev, [permKey]: !prev[permKey] }))
  }

  if (!isAdmin) return null

  return (
    <div className="p-4 md:p-8 max-w-7xl mx-auto">
      <PageHeader title="角色管理" description="管理系统角色及权限矩阵">
        <Button onClick={openCreate}>
          <Plus className="w-4 h-4 mr-1.5" />
          新建角色
        </Button>
      </PageHeader>

      {/* 角色列表 */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
        {isLoading && roles.length === 0 ? (
          <div className="col-span-full py-12 text-center text-gray-400">
            <Shield className="w-8 h-8 mx-auto mb-2 opacity-50" />
            加载中...
          </div>
        ) : roles.length === 0 ? (
          <div className="col-span-full py-12 text-center text-gray-400">
            <Shield className="w-8 h-8 mx-auto mb-2 opacity-50" />
            暂无角色
          </div>
        ) : (
          roles.map((role) => (
            <Card key={role.id} className="relative group">
              <CardContent className="pt-5">
                <div className="flex items-start justify-between mb-3">
                  <div className="flex items-center gap-2">
                    <Shield className="w-5 h-5 text-blue-600" />
                    <h3 className="text-base font-semibold text-gray-900">{role.name}</h3>
                  </div>
                  {role.isSystem && (
                    <span className="text-[10px] px-1.5 py-0.5 rounded bg-gray-100 text-gray-500 font-medium">
                      内置
                    </span>
                  )}
                </div>
                <p className="text-sm text-gray-500 mb-4 min-h-[1.25rem]">
                  {role.description || '无描述'}
                </p>
                <div className="flex items-center text-xs text-gray-500 mb-4">
                  <Users className="w-3.5 h-3.5 mr-1" />
                  {role.userCount} 位用户
                </div>
                <div className="flex items-center gap-2">
                  <Button
                    variant="secondary"
                    size="sm"
                    className="flex-1"
                    onClick={() => openMatrix(role)}
                  >
                    权限配置
                  </Button>
                  {!role.isSystem && (
                    <>
                      <button
                        onClick={() => openEdit(role)}
                        className="p-1.5 rounded-md text-gray-500 hover:text-blue-600 hover:bg-blue-50 transition-colors"
                        title="编辑"
                      >
                        <Edit2 className="w-3.5 h-3.5" />
                      </button>
                      <button
                        onClick={() => setConfirmDelete(role)}
                        className="p-1.5 rounded-md text-gray-500 hover:text-red-600 hover:bg-red-50 transition-colors"
                        title="删除"
                      >
                        <Trash2 className="w-3.5 h-3.5" />
                      </button>
                    </>
                  )}
                </div>
              </CardContent>
            </Card>
          ))
        )}
      </div>

      {/* 新建/编辑弹窗 */}
      <Dialog
        open={dialogOpen}
        onClose={() => {
          setDialogOpen(false)
          setEditingRole(null)
        }}
        title={dialogMode === 'create' ? '新建角色' : '编辑角色'}
        footer={
          <>
            <Button
              variant="ghost"
              onClick={() => {
                setDialogOpen(false)
                setEditingRole(null)
              }}
            >
              取消
            </Button>
            <Button onClick={handleSave}>保存</Button>
          </>
        }
      >
        <div className="space-y-4">
          <Input
            label="角色名称"
            value={form.name}
            onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))}
          />
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">描述</label>
            <textarea
              className="w-full px-3 py-2 rounded-lg border border-gray-300 bg-white text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 min-h-[80px]"
              value={form.description}
              onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))}
              placeholder="请输入角色描述"
            />
          </div>
        </div>
      </Dialog>

      {/* 删除确认 */}
      <Dialog
        open={confirmDelete !== null}
        onClose={() => setConfirmDelete(null)}
        title="确认删除角色"
        footer={
          <>
            <Button variant="ghost" onClick={() => setConfirmDelete(null)}>
              取消
            </Button>
            <Button variant="danger" onClick={handleDelete}>删除</Button>
          </>
        }
      >
        <p className="text-sm text-gray-600">
          确定要删除角色「{confirmDelete?.name}」吗？此操作不可恢复。
        </p>
      </Dialog>

      {/* 权限矩阵配置弹窗 */}
      <Dialog
        open={matrixRole !== null}
        onClose={() => setMatrixRole(null)}
        title={`权限配置 - ${matrixRole?.name}`}
        footer={
          <>
            <Button variant="ghost" onClick={() => setMatrixRole(null)}>
              取消
            </Button>
            <Button onClick={handleSaveMatrix} isLoading={savingMatrix}>
              <Save className="w-4 h-4 mr-1.5" />
              保存权限
            </Button>
          </>
        }
      >
        <div className="max-h-[60vh] overflow-auto">
          <table className="w-full text-sm border-collapse">
            <thead className="sticky top-0 bg-gray-50">
              <tr className="border-b border-gray-200">
                <th className="px-3 py-2 text-left font-medium text-gray-600 w-20">类别</th>
                <th className="px-3 py-2 text-left font-medium text-gray-600">资源项</th>
                <th className="px-3 py-2 text-center font-medium text-gray-600 w-20">查看</th>
                <th className="px-3 py-2 text-center font-medium text-gray-600 w-24">新建/编辑</th>
                <th className="px-3 py-2 text-center font-medium text-gray-600 w-20">删除</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {matrixRows.map((row) => (
                <tr key={row.resource + row.perms[0].key} className="hover:bg-gray-50/50">
                  <td className="px-3 py-2 text-gray-500 text-xs font-medium">{row.category}</td>
                  <td className="px-3 py-2 text-gray-800">{row.resource}</td>
                  {row.perms.map((perm) => (
                    <td key={perm.key} className="px-3 py-2 text-center">
                      <button
                        onClick={() => toggleCell(perm.key)}
                        className="inline-flex items-center justify-center text-gray-500 hover:text-blue-600 transition-colors"
                      >
                        {matrix[perm.key] ? (
                          <CheckSquare className="w-5 h-5 text-blue-600" />
                        ) : (
                          <Square className="w-5 h-5 text-gray-300" />
                        )}
                      </button>
                    </td>
                  ))}
                  {/* 补齐空白单元格使列对齐 */}
                  {Array.from({ length: 3 - row.perms.length }).map((_, i) => (
                    <td key={`empty-${i}`} className="px-3 py-2" />
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </Dialog>
    </div>
  )
}
