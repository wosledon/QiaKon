import { useAuth } from '@/stores/authStore'
import { Navigate, Outlet } from 'react-router-dom'

export function AdminGuard() {
  const { user, isAuthenticated } = useAuth()

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />
  }

  const isAdmin = user?.role === 'Admin'
  if (!isAdmin) {
    return <Navigate to="/" replace />
  }

  return <Outlet />
}
