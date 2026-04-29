import { useState, useEffect, useCallback } from 'react'
import { useNavigate } from 'react-router-dom'
import { Button } from '@/components/ui/Button'
import { Dialog } from '@/components/ui/Dialog'
import { Input } from '@/components/ui/Input'
import { Card, CardContent, CardHeader } from '@/components/ui/Card'
import { PageHeader } from '@/components/shared/PageHeader'
import { adminApi } from '@/services/api'
import { isAdminRole, useAuth } from '@/stores/authStore'
import type { AdminUser, Department } from '@/types'
import {
  Plus,
  Edit2,
  Trash2,
  ChevronRight,
  ChevronDown,
  Building2,
  Users,
  FolderOpen,
} from 'lucide-react'

interface TreeNode extends Department {
  level: number
  expanded: boolean
  children: TreeNode[]
}

function buildTree(departments: Department[], parentId?: string, level = 0): TreeNode[] {
  return departments
    .filter((d) => d.parentId === parentId)
    .map((d) => ({
      ...d,
      level,
      expanded: true,
      children: buildTree(departments, d.id, level + 1),
    }))
}

function flattenTree(nodes: TreeNode[], expandedIds: string[]): TreeNode[] {
  const result: TreeNode[] = []
  const walk = (items: TreeNode[]) => {
    items.forEach((node) => {
      result.push(node)
      if (expandedIds.includes(node.id) && node.children.length > 0) {
        walk(node.children)
      }
    })
  }
  walk(nodes)
  return result
}

export function AdminDepartmentsPage() {
  const navigate = useNavigate()
  const { user: currentUser } = useAuth()
  const isAdmin = isAdminRole(currentUser?.role)

  const [departments, setDepartments] = useState<Department[]>([])
  const [tree, setTree] = useState<TreeNode[]>([])
  const [expandedIds, setExpandedIds] = useState<Set<string>>(new Set())
  const [selectedDeptId, setSelectedDeptId] = useState<string | null>(null)
  const [members, setMembers] = useState<AdminUser[]>([])
  const [isLoading, setIsLoading] = useState(false)
  const [membersLoading, setMembersLoading] = useState(false)

  const [dialogOpen, setDialogOpen] = useState(false)
  const [dialogMode, setDialogMode] = useState<'create' | 'edit'>('create')
  const [editingDept, setEditingDept] = useState<Department | null>(null)
  const [confirmDelete, setConfirmDelete] = useState<Department | null>(null)

  const [form, setForm] = useState({ name: '', parentId: '' })

  useEffect(() => {
    if (!isAdmin) {
      navigate('/', { replace: true })
    }
  }, [isAdmin, navigate])

  const loadDepartments = useCallback(async () => {
    setIsLoading(true)
    try {
      const data = await adminApi.departments.list()
      setDepartments(data)
      const t = buildTree(data)
      setTree(t)
      // 默认展开所有
      const allIds = new Set<string>()
      const collect = (nodes: TreeNode[]) => {
        nodes.forEach((n) => {
          allIds.add(n.id)
          collect(n.children)
        })
      }
      collect(t)
      setExpandedIds(allIds)
    } catch (err) {
      console.error(err)
    } finally {
      setIsLoading(false)
    }
  }, [])

  useEffect(() => {
    if (isAdmin) loadDepartments()
  }, [loadDepartments, isAdmin])

  const loadMembers = useCallback(async (deptId: string) => {
    setMembersLoading(true)
    try {
      const data = await adminApi.departments.members(deptId)
      setMembers(data)
    } catch (err) {
      console.error(err)
      setMembers([])
    } finally {
      setMembersLoading(false)
    }
  }, [])

  useEffect(() => {
    if (selectedDeptId) {
      loadMembers(selectedDeptId)
    } else {
      setMembers([])
    }
  }, [selectedDeptId, loadMembers])

  const toggleExpand = (id: string) => {
    setExpandedIds((prev) => {
      const next = new Set(prev)
      if (next.has(id)) {
        next.delete(id)
      } else {
        next.add(id)
      }
      return next
    })
  }

  const openCreate = (parentId?: string) => {
    setForm({ name: '', parentId: parentId || '' })
    setDialogMode('create')
    setEditingDept(null)
    setDialogOpen(true)
  }

  const openEdit = (dept: Department) => {
    setForm({ name: dept.name, parentId: dept.parentId || '' })
    setDialogMode('edit')
    setEditingDept(dept)
    setDialogOpen(true)
  }

  const handleSave = async () => {
    try {
      if (dialogMode === 'create') {
        await adminApi.departments.create({
          name: form.name,
          parentId: form.parentId || undefined,
        })
      } else if (editingDept) {
        await adminApi.departments.update(editingDept.id, {
          name: form.name,
          parentId: form.parentId || undefined,
        })
      }
      setDialogOpen(false)
      setEditingDept(null)
      await loadDepartments()
    } catch (err) {
      alert(err instanceof Error ? err.message : '操作失败')
    }
  }

  const handleDelete = async () => {
    if (!confirmDelete) return
    try {
      await adminApi.departments.delete(confirmDelete.id)
      setConfirmDelete(null)
      if (selectedDeptId === confirmDelete.id) {
        setSelectedDeptId(null)
      }
      await loadDepartments()
    } catch (err) {
      alert(err instanceof Error ? err.message : '删除失败')
    }
  }

  const selectedDept = departments.find((d) => d.id === selectedDeptId)
  const flatTree = flattenTree(
    tree.map((t) => ({ ...t, expanded: expandedIds.has(t.id) })),
    Array.from(expandedIds)
  )

  if (!isAdmin) return null

  return (
    <div className="p-4 md:p-8 max-w-7xl mx-auto">
      <PageHeader title="部门管理" description="维护组织架构，支持多级部门">
        <Button onClick={() => openCreate()}>
          <Plus className="w-4 h-4 mr-1.5" />
          新建部门
        </Button>
      </PageHeader>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* 左侧部门树 */}
        <Card className="lg:col-span-1">
          <CardHeader>
            <div className="flex items-center gap-2">
              <Building2 className="w-4 h-4 text-gray-500" />
              <h3 className="text-sm font-semibold text-gray-900">部门结构</h3>
            </div>
          </CardHeader>
          <CardContent className="pt-0">
            {isLoading && departments.length === 0 ? (
              <div className="py-8 text-center text-gray-400 text-sm">加载中...</div>
            ) : departments.length === 0 ? (
              <div className="py-8 text-center text-gray-400 text-sm">暂无部门</div>
            ) : (
              <div className="space-y-0.5">
                {flatTree.map((node) => (
                  <div
                    key={node.id}
                    className={`group flex items-center gap-1 rounded-lg px-2 py-1.5 cursor-pointer transition-colors ${
                      selectedDeptId === node.id
                        ? 'bg-blue-50 text-blue-700'
                        : 'hover:bg-gray-50 text-gray-700'
                    }`}
                    style={{ paddingLeft: `${node.level * 16 + 8}px` }}
                    onClick={() => setSelectedDeptId(node.id)}
                  >
                    <button
                      onClick={(e) => {
                        e.stopPropagation()
                        toggleExpand(node.id)
                      }}
                      className={`p-0.5 rounded transition-opacity ${
                        node.children.length > 0 ? 'opacity-100' : 'opacity-0 pointer-events-none'
                      }`}
                    >
                      {expandedIds.has(node.id) ? (
                        <ChevronDown className="w-3.5 h-3.5" />
                      ) : (
                        <ChevronRight className="w-3.5 h-3.5" />
                      )}
                    </button>
                    <FolderOpen className="w-4 h-4 flex-shrink-0" />
                    <span className="flex-1 text-sm truncate">{node.name}</span>
                    <div className="flex items-center gap-0.5 opacity-0 group-hover:opacity-100 transition-opacity">
                      <button
                        onClick={(e) => {
                          e.stopPropagation()
                          openCreate(node.id)
                        }}
                        className="p-1 rounded hover:bg-gray-200/50 text-gray-500"
                        title="添加子部门"
                      >
                        <Plus className="w-3 h-3" />
                      </button>
                      <button
                        onClick={(e) => {
                          e.stopPropagation()
                          openEdit(node)
                        }}
                        className="p-1 rounded hover:bg-gray-200/50 text-gray-500"
                        title="编辑"
                      >
                        <Edit2 className="w-3 h-3" />
                      </button>
                      <button
                        onClick={(e) => {
                          e.stopPropagation()
                          setConfirmDelete(node)
                        }}
                        className="p-1 rounded hover:bg-red-100 text-gray-500 hover:text-red-600"
                        title="删除"
                      >
                        <Trash2 className="w-3 h-3" />
                      </button>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </CardContent>
        </Card>

        {/* 右侧成员列表 */}
        <Card className="lg:col-span-2">
          <CardHeader>
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-2">
                <Users className="w-4 h-4 text-gray-500" />
                <h3 className="text-sm font-semibold text-gray-900">
                  {selectedDept ? `${selectedDept.name} - 成员列表` : '请选择部门'}
                </h3>
              </div>
              {selectedDept && (
                <span className="text-xs text-gray-500">{members.length} 人</span>
              )}
            </div>
          </CardHeader>
          <CardContent className="pt-0">
            {!selectedDeptId ? (
              <div className="py-16 text-center text-gray-400 text-sm">
                <Building2 className="w-10 h-10 mx-auto mb-3 opacity-30" />
                点击左侧部门查看成员
              </div>
            ) : membersLoading ? (
              <div className="py-16 text-center text-gray-400 text-sm">加载中...</div>
            ) : members.length === 0 ? (
              <div className="py-16 text-center text-gray-400 text-sm">该部门暂无成员</div>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full text-sm">
                  <thead className="bg-gray-50 border-b border-gray-200">
                    <tr>
                      <th className="px-4 py-3 text-left font-medium text-gray-600">用户名</th>
                      <th className="px-4 py-3 text-left font-medium text-gray-600">邮箱</th>
                      <th className="px-4 py-3 text-left font-medium text-gray-600">角色</th>
                      <th className="px-4 py-3 text-left font-medium text-gray-600">状态</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-100">
                    {members.map((m) => (
                      <tr key={m.id} className="hover:bg-gray-50/50">
                        <td className="px-4 py-3 font-medium text-gray-900">{m.username}</td>
                        <td className="px-4 py-3 text-gray-600">{m.email}</td>
                        <td className="px-4 py-3">
                          <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-gray-100 text-gray-700">
                            {m.role}
                          </span>
                        </td>
                        <td className="px-4 py-3">
                          <span
                            className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium ${
                              m.status === 'active'
                                ? 'bg-green-100 text-green-700'
                                : 'bg-gray-100 text-gray-500'
                            }`}
                          >
                            {m.status === 'active' ? '已启用' : '已禁用'}
                          </span>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </CardContent>
        </Card>
      </div>

      {/* 新建/编辑弹窗 */}
      <Dialog
        open={dialogOpen}
        onClose={() => {
          setDialogOpen(false)
          setEditingDept(null)
        }}
        title={dialogMode === 'create' ? '新建部门' : '编辑部门'}
        footer={
          <>
            <Button
              variant="ghost"
              onClick={() => {
                setDialogOpen(false)
                setEditingDept(null)
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
            label="部门名称"
            value={form.name}
            onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))}
          />
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">上级部门</label>
            <select
              className="w-full px-3 py-2 rounded-lg border border-gray-300 bg-white text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
              value={form.parentId}
              onChange={(e) => setForm((f) => ({ ...f, parentId: e.target.value }))}
            >
              <option value="">无（顶级部门）</option>
              {departments.map((d) => (
                <option key={d.id} value={d.id}>
                  {d.name}
                </option>
              ))}
            </select>
          </div>
        </div>
      </Dialog>

      {/* 删除确认 */}
      <Dialog
        open={confirmDelete !== null}
        onClose={() => setConfirmDelete(null)}
        title="确认删除部门"
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
          确定要删除部门「{confirmDelete?.name}」吗？该部门下的用户将失去部门归属。
        </p>
      </Dialog>
    </div>
  )
}
