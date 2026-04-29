import { useState, useEffect, useCallback } from 'react'
import { PageHeader } from '@/components/shared/PageHeader'
import { Card, CardHeader, CardContent } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { Input } from '@/components/ui/Input'
import { useAuth } from '@/stores/authStore'
import { profileApi } from '@/services/api'
import type { ProfileUpdateData, PasswordChangeData } from '@/types'
import { User, Mail, Building2, Shield, LogOut, Save, Lock } from 'lucide-react'
import { useNavigate } from 'react-router-dom'

export function ProfilePage() {
  const { user, logout } = useAuth()
  const navigate = useNavigate()
  const [isEditing, setIsEditing] = useState(false)
  const [saving, setSaving] = useState(false)

  const [profileForm, setProfileForm] = useState<ProfileUpdateData>({
    displayName: user?.displayName || '',
    email: user?.email || '',
    departmentName: user?.departmentName || '',
  })

  const [passwordForm, setPasswordForm] = useState<PasswordChangeData>({
    currentPassword: '',
    newPassword: '',
    confirmPassword: '',
  })
  const [passwordErrors, setPasswordErrors] = useState<Partial<Record<keyof PasswordChangeData, string>>>({})
  const [changingPassword, setChangingPassword] = useState(false)

  useEffect(() => {
    if (user) {
      setProfileForm({
        displayName: user.displayName || '',
        email: user.email || '',
        departmentName: user.departmentName || '',
      })
    }
  }, [user])

  const fetchProfile = useCallback(async () => {
    try {
      const data = await profileApi.get()
      setProfileForm({
        displayName: data.displayName || '',
        email: data.email || '',
        departmentName: data.departmentName || '',
      })
    } catch {
      // ignore
    }
  }, [])

  useEffect(() => {
    fetchProfile()
  }, [fetchProfile])

  const saveProfile = async () => {
    setSaving(true)
    try {
      await profileApi.update(profileForm)
      setIsEditing(false)
      alert('保存成功')
      fetchProfile()
    } catch (e: unknown) {
      alert(e instanceof Error ? e.message : '保存失败')
    } finally {
      setSaving(false)
    }
  }

  const validatePassword = (): boolean => {
    const errors: Partial<Record<keyof PasswordChangeData, string>> = {}
    if (!passwordForm.currentPassword) errors.currentPassword = '请输入原密码'
    if (!passwordForm.newPassword) errors.newPassword = '请输入新密码'
    else if (passwordForm.newPassword.length < 6) errors.newPassword = '密码至少 6 位'
    if (passwordForm.newPassword !== passwordForm.confirmPassword) {
      errors.confirmPassword = '两次输入的密码不一致'
    }
    setPasswordErrors(errors)
    return Object.keys(errors).length === 0
  }

  const changePassword = async () => {
    if (!validatePassword()) return
    setChangingPassword(true)
    try {
      await profileApi.changePassword({
        currentPassword: passwordForm.currentPassword,
        newPassword: passwordForm.newPassword,
      })
      setPasswordForm({ currentPassword: '', newPassword: '', confirmPassword: '' })
      alert('密码修改成功')
    } catch (e: unknown) {
      alert(e instanceof Error ? e.message : '密码修改失败')
    } finally {
      setChangingPassword(false)
    }
  }

  const handleLogout = async () => {
    try {
      await profileApi.logout()
    } catch {
      // ignore
    } finally {
      logout()
      navigate('/login')
    }
  }

  return (
    <div>
      <PageHeader title="个人中心" description="查看和管理个人信息">
        <Button variant="danger" size="sm" onClick={handleLogout}>
          <LogOut className="w-4 h-4 mr-1" /> 退出登录
        </Button>
      </PageHeader>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <Card className="lg:col-span-1">
          <CardContent className="pt-6">
            <div className="flex flex-col items-center">
              <div className="w-24 h-24 rounded-full bg-gradient-to-br from-blue-500 to-indigo-600 flex items-center justify-center text-white text-3xl font-bold shadow-lg">
                {(user?.displayName?.[0] || user?.username?.[0] || 'U')}
              </div>
              <h3 className="mt-4 text-lg font-semibold text-gray-900">{user?.displayName || user?.username || '用户'}</h3>
              <p className="text-sm text-gray-500">{user?.email || '-'}</p>
            </div>
          </CardContent>
        </Card>

        <div className="lg:col-span-2 space-y-6">
          <Card>
            <CardHeader>
              <div className="flex items-center justify-between">
                <h3 className="text-base font-semibold text-gray-900">基本信息</h3>
                <Button variant="ghost" size="sm" onClick={() => isEditing ? setIsEditing(false) : setIsEditing(true)}>
                  {isEditing ? '取消' : '编辑'}
                </Button>
              </div>
            </CardHeader>
            <CardContent>
              {isEditing ? (
                <div className="space-y-4">
                  <div className="flex items-center gap-3">
                    <User className="w-4 h-4 text-gray-400 flex-shrink-0" />
                    <div className="flex-1">
                      <label className="block text-xs text-gray-400 mb-1">用户名</label>
                      <p className="text-sm text-gray-900">{user?.username || '-'}</p>
                    </div>
                  </div>
                  <div className="flex items-center gap-3">
                    <Mail className="w-4 h-4 text-gray-400 flex-shrink-0" />
                    <div className="flex-1">
                      <Input
                        label="邮箱"
                        value={profileForm.email || ''}
                        onChange={e => setProfileForm(prev => ({ ...prev, email: e.target.value }))}
                      />
                    </div>
                  </div>
                  <div className="flex items-center gap-3">
                    <Building2 className="w-4 h-4 text-gray-400 flex-shrink-0" />
                    <div className="flex-1">
                      <Input
                        label="部门"
                        value={profileForm.departmentName || ''}
                        onChange={e => setProfileForm(prev => ({ ...prev, departmentName: e.target.value }))}
                      />
                    </div>
                  </div>
                  <div className="flex items-center gap-3">
                    <Shield className="w-4 h-4 text-gray-400 flex-shrink-0" />
                    <div className="flex-1">
                      <label className="block text-xs text-gray-400 mb-1">角色</label>
                      <p className="text-sm text-gray-900">{user?.role || '-'}</p>
                    </div>
                  </div>
                  <div className="flex justify-end pt-2">
                    <Button size="sm" onClick={saveProfile} isLoading={saving}>
                      <Save className="w-4 h-4 mr-1" /> 保存
                    </Button>
                  </div>
                </div>
              ) : (
                <div className="space-y-4">
                  <div className="flex items-center gap-3 py-3 border-b border-gray-50">
                    <User className="w-4 h-4 text-gray-400" />
                    <div className="flex-1">
                      <p className="text-xs text-gray-400">用户名</p>
                      <p className="text-sm text-gray-900">{user?.username || '-'}</p>
                    </div>
                  </div>
                  <div className="flex items-center gap-3 py-3 border-b border-gray-50">
                    <Mail className="w-4 h-4 text-gray-400" />
                    <div className="flex-1">
                      <p className="text-xs text-gray-400">邮箱</p>
                      <p className="text-sm text-gray-900">{user?.email || '-'}</p>
                    </div>
                  </div>
                  <div className="flex items-center gap-3 py-3 border-b border-gray-50">
                    <Building2 className="w-4 h-4 text-gray-400" />
                    <div className="flex-1">
                      <p className="text-xs text-gray-400">部门</p>
                      <p className="text-sm text-gray-900">{user?.departmentName || user?.departmentId || '-'}</p>
                    </div>
                  </div>
                  <div className="flex items-center gap-3 py-3">
                    <Shield className="w-4 h-4 text-gray-400" />
                    <div className="flex-1">
                      <p className="text-xs text-gray-400">角色</p>
                      <p className="text-sm text-gray-900">{user?.role || '-'}</p>
                    </div>
                  </div>
                </div>
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <h3 className="text-base font-semibold text-gray-900 flex items-center gap-2">
                <Lock className="w-4 h-4 text-gray-400" />
                修改密码
              </h3>
            </CardHeader>
            <CardContent className="space-y-4">
              <Input
                label="原密码"
                type="password"
                value={passwordForm.currentPassword}
                onChange={e => setPasswordForm(prev => ({ ...prev, currentPassword: e.target.value }))}
                error={passwordErrors.currentPassword}
              />
              <Input
                label="新密码"
                type="password"
                value={passwordForm.newPassword}
                onChange={e => setPasswordForm(prev => ({ ...prev, newPassword: e.target.value }))}
                error={passwordErrors.newPassword}
              />
              <Input
                label="确认密码"
                type="password"
                value={passwordForm.confirmPassword}
                onChange={e => setPasswordForm(prev => ({ ...prev, confirmPassword: e.target.value }))}
                error={passwordErrors.confirmPassword}
              />
              <div className="flex justify-end">
                <Button onClick={changePassword} isLoading={changingPassword}>
                  修改密码
                </Button>
              </div>
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  )
}
