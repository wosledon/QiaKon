import { useState, type FormEvent, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { Button } from '@/components/ui/Button'
import { Input } from '@/components/ui/Input'
import { authApi } from '@/services/api'
import { useAuth } from '@/stores/authStore'

export function LoginPage() {
  const { login, isAuthenticated } = useAuth()
  const navigate = useNavigate()
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [isLoading, setIsLoading] = useState(false)

  // 已登录用户自动跳转首页
  useEffect(() => {
    if (isAuthenticated) {
      navigate('/', { replace: true })
    }
  }, [isAuthenticated, navigate])

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault()
    setError('')

    if (!username.trim() || !password.trim()) {
      setError('请输入用户名和密码')
      return
    }

    setIsLoading(true)
    try {
      const response = await authApi.login({ username, password })
      login(response)
      navigate('/', { replace: true })
    } catch (err) {
      const message = err instanceof Error ? err.message : '登录失败，请重试'
      setError(message)
    } finally {
      setIsLoading(false)
    }
  }

  return (
    <div className="min-h-screen flex items-center justify-center bg-gray-50 px-4">
      <div className="w-full max-w-md">
        <div className="text-center mb-8">
          <h1 className="text-3xl font-bold text-gray-900">QiaKon</h1>
          <p className="mt-2 text-gray-600">企业级知识图谱问答平台</p>
        </div>

        <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-8">
          <h2 className="text-xl font-semibold text-gray-900 mb-6 text-center">用户登录</h2>

          <form onSubmit={handleSubmit} className="space-y-5">
            <Input
              label="用户名"
              type="text"
              placeholder="请输入用户名"
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              autoComplete="username"
            />
            <Input
              label="密码"
              type="password"
              placeholder="请输入密码"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              autoComplete="current-password"
            />

            {error && (
              <div className="rounded-lg bg-red-50 px-4 py-3 text-sm text-red-600">
                {error}
              </div>
            )}

            <Button type="submit" isLoading={isLoading} className="w-full">
              登录
            </Button>
          </form>

          <div className="mt-6 rounded-lg bg-gray-50 border border-gray-100 px-4 py-3">
            <p className="text-xs text-gray-500 mb-1 font-medium">演示账号</p>
            <div className="flex items-center justify-between text-sm text-gray-700">
              <span>admin / password123</span>
              <button
                type="button"
                className="text-blue-600 hover:text-blue-700 text-xs font-medium"
                onClick={() => {
                  setUsername('admin')
                  setPassword('password123')
                }}
              >
                一键填充
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>
  )
}
