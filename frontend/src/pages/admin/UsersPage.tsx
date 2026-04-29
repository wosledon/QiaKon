import { useState, useEffect, useCallback } from 'react'
import { useNavigate } from 'react-router-dom'
import { Button } from '@/components/ui/Button'
import { Input } from '@/components/ui/Input'
import { Dialog } from '@/components/ui/Dialog'
import { Card, CardContent } from '@/components/ui/Card'
import { PageHeader } from '@/components/shared/PageHeader'
import { adminApi } from '@/services/api'
import { isAdminRole, useAuth } from '@/stores/authStore'
import type { AdminUser, Department, Role } from '@/types'
import {
  Search,
  Plus,
  Edit2,
  Trash2,
  Lock,
  Power,
  PowerOff,
  ChevronLeft,
  ChevronRight,
  Users,
  CheckSquare,
  Square,
} from 'lucide-react'

const roleColors: Record<string, string> = {
  Admin: 'bg-red-100 text-red-700',
  KnowledgeAdmin: 'bg-orange-100 text-orange-700',
  DepartmentManager: 'bg-blue-100 text-blue-700',
  DepartmentMember: 'bg-gray-100 text-gray-700',
  Guest: 'bg-gray-100 text-gray-700',
}

const statusColors: Record<string, string> = {
  active: 'bg-green-100 text-green-700',
  inactive: 'bg-gray-100 text-gray-500',
}

export function AdminUsersPage() {
  const navigate = useNavigate()
  const { user: currentUser } = useAuth()

  const [users, setUsers] = useState<AdminUser[]>([])
  const [totalCount, setTotalCount] = useState(0)
  const [page, setPage] = useState(1)
  const [pageSize] = useState(10)
  const [isLoading, setIsLoading] = useState(false)
  const [departments, setDepartments] = useState<Department[]>([])
  const [roles, setRoles] = useState<Role[]>([])

  const [filters, setFilters] = useState({
    departmentId: '',
    role: '',
    status: '',
    keyword: '',
  })

  const [selectedIds, setSelectedIds] = useState<string[]>([])
  const [dialogType, setDialogType] = useState<'create' | 'edit' | null>(null)
  const [editingUser, setEditingUser] = useState<AdminUser | null>(null)
  const [confirmAction, setConfirmAction] = useState<{
    type: 'delete' | 'reset' | 'batch'
    ids: string[]
    operation?: 'enable' | 'disable' | 'delete'
  } | null>(null)

  const [form, setForm] = useState({
    username: '',
    email: '',
    password: '',
    departmentId: '',
    role: 'DepartmentMember',
  })

  const isAdmin = isAdminRole(currentUser?.role)

  // 权限检查
  useEffect(() => {
    if (!isAdmin) {
      navigate('/', { replace: true })
    }
  }, [isAdmin, navigate])

  const loadUsers = useCallback(async () => {
    setIsLoading(true)
    try {
      const data = await adminApi.users.list({
        departmentId: filters.departmentId || undefined,
        role: filters.role || undefined,
        status: (filters.status as 'active' | 'inactive') || undefined,
        keyword: filters.keyword || undefined,
        page,
        pageSize,
      })
      setUsers(data.items)
      setTotalCount(data.totalCount)
      setSelectedIds([])
    } catch (err) {
      console.error(err)
    } finally {
      setIsLoading(false)
    }
  }, [filters, page, pageSize])

  const loadMeta = useCallback(async () => {
    try {
      const [depts, rolesData] = await Promise.all([
        adminApi.departments.list(),
        adminApi.roles.list(),
      ])
      setDepartments(flattenDepartments(depts))
      setRoles(rolesData)
    } catch (err) {
      console.error(err)
    }
  }, [])

  useEffect(() => {
    if (isAdmin) {
      loadUsers()
      loadMeta()
    }
  }, [loadUsers, loadMeta, isAdmin])

  const flattenDepartments = (depts: Department[]): Department[] => {
    const result: Department[] = []
    const walk = (items: Department[], prefix = '') => {
      items.forEach((d) => {
        result.push({ ...d, name: prefix + d.name })
        if (d.children?.length) walk(d.children, prefix + '　')
      })
    }
    walk(depts)
    return result
  }

  const handleFilterChange = (key: string, value: string) => {
    setFilters((prev) => ({ ...prev, [key]: value }))
    setPage(1)
  }

  const toggleSelect = (id: string) => {
    setSelectedIds((prev) =>
      prev.includes(id) ? prev.filter((x) => x !== id) : [...prev, id]
    )
  }

  const toggleSelectAll = () => {
    if (selectedIds.length === users.length) {
      setSelectedIds([])
    } else {
      setSelectedIds(users.map((u) => u.id))
    }
  }

  const openCreate = () => {
    setForm({
      username: '',
      email: '',
      password: '',
      departmentId: '',
      role: 'DepartmentMember',
    })
    setDialogType('create')
  }

  const openEdit = (user: AdminUser) => {
    setEditingUser(user)
    setForm({
      username: user.username,
      email: user.email,
      password: '',
      departmentId: user.departmentId || '',
      role: user.role,
    })
    setDialogType('edit')
  }

  const handleSave = async () => {
    try {
      if (dialogType === 'create') {
        await adminApi.users.create({
          username: form.username,
          email: form.email,
          password: form.password,
          departmentId: form.departmentId || undefined,
          role: form.role,
        })
      } else if (dialogType === 'edit' && editingUser) {
        await adminApi.users.update(editingUser.id, {
          departmentId: form.departmentId || undefined,
          role: form.role,
        })
      }
      setDialogType(null)
      setEditingUser(null)
      await loadUsers()
    } catch (err) {
      alert(err instanceof Error ? err.message : '操作失败')
    }
  }

  const handleDelete = async (id: string) => {
    setConfirmAction({ type: 'delete', ids: [id] })
  }

  const handleResetPassword = async (id: string) => {
    setConfirmAction({ type: 'reset', ids: [id] })
  }

  const handleToggleStatus = async (id: string, status: 'active' | 'inactive') => {
    try {
      await adminApi.users.updateStatus(id, status)
      await loadUsers()
    } catch (err) {
      alert(err instanceof Error ? err.message : '操作失败')
    }
  }

  const handleBatch = (operation: 'enable' | 'disable' | 'delete') => {
    setConfirmAction({ type: 'batch', ids: selectedIds, operation })
  }

  const executeConfirm = async () => {
    if (!confirmAction) return
    try {
      if (confirmAction.type === 'delete' || (confirmAction.type === 'batch' && confirmAction.operation === 'delete')) {
        await adminApi.users.batch({ ids: confirmAction.ids, operation: 'delete' })
      } else if (confirmAction.type === 'reset') {
        for (const id of confirmAction.ids) {
          await adminApi.users.resetPassword(id)
        }
      } else if (confirmAction.type === 'batch' && confirmAction.operation) {
        await adminApi.users.batch({
          ids: confirmAction.ids,
          operation: confirmAction.operation,
        })
      }
      setConfirmAction(null)
      await loadUsers()
    } catch (err) {
      alert(err instanceof Error ? err.message : '操作失败')
    }
  }

  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize))

  const confirmTitle =
    confirmAction?.type === 'delete'
      ? '确认删除'
      : confirmAction?.type === 'reset'
      ? '确认重置密码'
      : confirmAction?.operation === 'enable'
      ? '确认批量启用'
      : confirmAction?.operation === 'disable'
      ? '确认批量禁用'
      : '确认批量删除'

  const confirmMessage =
    confirmAction?.type === 'delete'
      ? `确定要删除选中的 ${confirmAction.ids.length} 个用户吗？此操作不可恢复。`
      : confirmAction?.type === 'reset'
      ? `确定要重置选中的 ${confirmAction.ids.length} 个用户的密码吗？`
      : confirmAction?.operation === 'enable'
      ? `确定要启用选中的 ${confirmAction.ids.length} 个用户吗？`
      : confirmAction?.operation === 'disable'
      ? `确定要禁用选中的 ${confirmAction.ids.length} 个用户吗？`
      : `确定要删除选中的 ${confirmAction?.ids.length} 个用户吗？`

  if (!isAdmin) return null

  return (
    <div className="p-4 md:p-8 max-w-7xl mx-auto">
      <PageHeader title="用户管理" description="管理用户账号、角色与部门归属">
        <Button onClick={openCreate}>
          <Plus className="w-4 h-4 mr-1.5" />
          新建用户
        </Button>
      </PageHeader>

      {/* 筛选栏 */}
      <Card className="mb-6">
        <CardContent>
          <div className="flex flex-wrap gap-3 items-end">
            <div className="w-full sm:w-48">
              <label className="block text-xs font-medium text-gray-500 mb-1">部门</label>
              <select
                className="w-full px-3 py-2 rounded-lg border border-gray-300 bg-white text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
                value={filters.departmentId}
                onChange={(e) => handleFilterChange('departmentId', e.target.value)}
              >
                <option value="">全部部门</option>
                {departments.map((d) => (
                  <option key={d.id} value={d.id}>
                    {d.name}
                  </option>
                ))}
              </select>
            </div>
            <div className="w-full sm:w-40">
              <label className="block text-xs font-medium text-gray-500 mb-1">角色</label>
              <select
                className="w-full px-3 py-2 rounded-lg border border-gray-300 bg-white text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
                value={filters.role}
                onChange={(e) => handleFilterChange('role', e.target.value)}
              >
                <option value="">全部角色</option>
                {roles.map((r) => (
                  <option key={r.id} value={r.name}>
                    {r.name}
                  </option>
                ))}
              </select>
            </div>
            <div className="w-full sm:w-36">
              <label className="block text-xs font-medium text-gray-500 mb-1">状态</label>
              <select
                className="w-full px-3 py-2 rounded-lg border border-gray-300 bg-white text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
                value={filters.status}
                onChange={(e) => handleFilterChange('status', e.target.value)}
              >
                <option value="">全部状态</option>
                <option value="active">已启用</option>
                <option value="inactive">已禁用</option>
              </select>
            </div>
            <div className="flex-1 min-w-[200px]">
              <label className="block text-xs font-medium text-gray-500 mb-1">搜索</label>
              <div className="relative">
                <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
                <input
                  type="text"
                  placeholder="用户名 / 邮箱"
                  className="w-full pl-9 pr-3 py-2 rounded-lg border border-gray-300 bg-white text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
                  value={filters.keyword}
                  onChange={(e) => handleFilterChange('keyword', e.target.value)}
                />
              </div>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* 批量操作 */}
      {selectedIds.length > 0 && (
        <div className="mb-4 flex items-center gap-3 bg-blue-50 border border-blue-200 rounded-lg px-4 py-3">
          <span className="text-sm text-blue-800 font-medium">
            已选择 {selectedIds.length} 项
          </span>
          <div className="flex-1" />
          <Button variant="secondary" size="sm" onClick={() => handleBatch('enable')}>
            <Power className="w-3.5 h-3.5 mr-1" />
            批量启用
          </Button>
          <Button variant="secondary" size="sm" onClick={() => handleBatch('disable')}>
            <PowerOff className="w-3.5 h-3.5 mr-1" />
            批量禁用
          </Button>
          <Button variant="danger" size="sm" onClick={() => handleBatch('delete')}>
            <Trash2 className="w-3.5 h-3.5 mr-1" />
            批量删除
          </Button>
          <button
            className="text-blue-600 hover:text-blue-800 text-sm"
            onClick={() => setSelectedIds([])}
          >
            取消选择
          </button>
        </div>
      )}

      {/* 表格 */}
      <Card>
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="bg-gray-50 border-b border-gray-200">
              <tr>
                <th className="px-4 py-3 w-10">
                  <button onClick={toggleSelectAll} className="text-gray-500 hover:text-blue-600">
                    {selectedIds.length === users.length && users.length > 0 ? (
                      <CheckSquare className="w-4.5 h-4.5" />
                    ) : (
                      <Square className="w-4.5 h-4.5" />
                    )}
                  </button>
                </th>
                <th className="px-4 py-3 text-left font-medium text-gray-600">用户名</th>
                <th className="px-4 py-3 text-left font-medium text-gray-600">邮箱</th>
                <th className="px-4 py-3 text-left font-medium text-gray-600">部门</th>
                <th className="px-4 py-3 text-left font-medium text-gray-600">角色</th>
                <th className="px-4 py-3 text-left font-medium text-gray-600">状态</th>
                <th className="px-4 py-3 text-left font-medium text-gray-600">最后登录</th>
                <th className="px-4 py-3 text-right font-medium text-gray-600">操作</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {isLoading && users.length === 0 ? (
                <tr>
                  <td colSpan={8} className="px-4 py-12 text-center text-gray-400">
                    <Users className="w-8 h-8 mx-auto mb-2 opacity-50" />
                    加载中...
                  </td>
                </tr>
              ) : users.length === 0 ? (
                <tr>
                  <td colSpan={8} className="px-4 py-12 text-center text-gray-400">
                    <Users className="w-8 h-8 mx-auto mb-2 opacity-50" />
                    暂无用户
                  </td>
                </tr>
              ) : (
                users.map((user) => (
                  <tr key={user.id} className="hover:bg-gray-50/50 transition-colors">
                    <td className="px-4 py-3">
                      <button
                        onClick={() => toggleSelect(user.id)}
                        className="text-gray-500 hover:text-blue-600"
                      >
                        {selectedIds.includes(user.id) ? (
                          <CheckSquare className="w-4.5 h-4.5" />
                        ) : (
                          <Square className="w-4.5 h-4.5" />
                        )}
                      </button>
                    </td>
                    <td className="px-4 py-3 font-medium text-gray-900">{user.username}</td>
                    <td className="px-4 py-3 text-gray-600">{user.email}</td>
                    <td className="px-4 py-3 text-gray-600">{user.departmentName || '-'}</td>
                    <td className="px-4 py-3">
                      <span
                        className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium ${
                          roleColors[user.role] || 'bg-gray-100 text-gray-700'
                        }`}
                      >
                        {user.role}
                      </span>
                    </td>
                    <td className="px-4 py-3">
                      <span
                        className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium ${
                          statusColors[user.status]
                        }`}
                      >
                        {user.status === 'active' ? '已启用' : '已禁用'}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-gray-500 text-xs">
                      {user.lastLoginAt
                        ? new Date(user.lastLoginAt).toLocaleString('zh-CN')
                        : '-'}
                    </td>
                    <td className="px-4 py-3 text-right">
                      <div className="flex items-center justify-end gap-1">
                        <button
                          onClick={() => openEdit(user)}
                          className="p-1.5 rounded-md text-gray-500 hover:text-blue-600 hover:bg-blue-50 transition-colors"
                          title="编辑"
                        >
                          <Edit2 className="w-3.5 h-3.5" />
                        </button>
                        <button
                          onClick={() => handleResetPassword(user.id)}
                          className="p-1.5 rounded-md text-gray-500 hover:text-amber-600 hover:bg-amber-50 transition-colors"
                          title="重置密码"
                        >
                          <Lock className="w-3.5 h-3.5" />
                        </button>
                        <button
                          onClick={() =>
                            handleToggleStatus(
                              user.id,
                              user.status === 'active' ? 'inactive' : 'active'
                            )
                          }
                          className={`p-1.5 rounded-md transition-colors ${
                            user.status === 'active'
                              ? 'text-gray-500 hover:text-orange-600 hover:bg-orange-50'
                              : 'text-gray-500 hover:text-green-600 hover:bg-green-50'
                          }`}
                          title={user.status === 'active' ? '禁用' : '启用'}
                        >
                          {user.status === 'active' ? (
                            <PowerOff className="w-3.5 h-3.5" />
                          ) : (
                            <Power className="w-3.5 h-3.5" />
                          )}
                        </button>
                        <button
                          onClick={() => handleDelete(user.id)}
                          className="p-1.5 rounded-md text-gray-500 hover:text-red-600 hover:bg-red-50 transition-colors"
                          title="删除"
                        >
                          <Trash2 className="w-3.5 h-3.5" />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>

        {/* 分页 */}
        <div className="flex items-center justify-between px-4 py-3 border-t border-gray-100">
          <span className="text-sm text-gray-500">
            共 {totalCount} 条，第 {page} / {totalPages} 页
          </span>
          <div className="flex items-center gap-2">
            <Button
              variant="ghost"
              size="sm"
              onClick={() => setPage((p) => Math.max(1, p - 1))}
              disabled={page <= 1}
            >
              <ChevronLeft className="w-4 h-4" />
            </Button>
            <span className="text-sm text-gray-600 px-2">
              {page} / {totalPages}
            </span>
            <Button
              variant="ghost"
              size="sm"
              onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
              disabled={page >= totalPages}
            >
              <ChevronRight className="w-4 h-4" />
            </Button>
          </div>
        </div>
      </Card>

      {/* 新建/编辑弹窗 */}
      <Dialog
        open={dialogType !== null}
        onClose={() => {
          setDialogType(null)
          setEditingUser(null)
        }}
        title={dialogType === 'create' ? '新建用户' : '编辑用户'}
        footer={
          <>
            <Button
              variant="ghost"
              onClick={() => {
                setDialogType(null)
                setEditingUser(null)
              }}
            >
              取消
            </Button>
            <Button onClick={handleSave}>
              {dialogType === 'create' ? '创建' : '保存'}
            </Button>
          </>
        }
      >
        <div className="space-y-4">
          {dialogType === 'create' && (
            <>
              <Input
                label="用户名"
                value={form.username}
                onChange={(e) => setForm((f) => ({ ...f, username: e.target.value }))}
              />
              <Input
                label="邮箱"
                type="email"
                value={form.email}
                onChange={(e) => setForm((f) => ({ ...f, email: e.target.value }))}
              />
              <Input
                label="初始密码"
                type="password"
                value={form.password}
                onChange={(e) => setForm((f) => ({ ...f, password: e.target.value }))}
              />
            </>
          )}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">部门</label>
            <select
              className="w-full px-3 py-2 rounded-lg border border-gray-300 bg-white text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
              value={form.departmentId}
              onChange={(e) => setForm((f) => ({ ...f, departmentId: e.target.value }))}
            >
              <option value="">请选择部门</option>
              {departments.map((d) => (
                <option key={d.id} value={d.id}>
                  {d.name}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">角色</label>
            <select
              className="w-full px-3 py-2 rounded-lg border border-gray-300 bg-white text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
              value={form.role}
              onChange={(e) => setForm((f) => ({ ...f, role: e.target.value }))}
            >
              {roles.map((r) => (
                <option key={r.id} value={r.name}>
                  {r.name}
                </option>
              ))}
            </select>
          </div>
        </div>
      </Dialog>

      {/* 确认对话框 */}
      <Dialog
        open={confirmAction !== null}
        onClose={() => setConfirmAction(null)}
        title={confirmTitle}
        footer={
          <>
            <Button variant="ghost" onClick={() => setConfirmAction(null)}>
              取消
            </Button>
            <Button
              variant={confirmAction?.type === 'reset' ? 'primary' : 'danger'}
              onClick={executeConfirm}
            >
              确认
            </Button>
          </>
        }
      >
        <p className="text-sm text-gray-600">{confirmMessage}</p>
      </Dialog>
    </div>
  )
}
