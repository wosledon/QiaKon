import { isAdminRole, useAuth } from '@/stores/authStore'
import { Navigate, Outlet } from 'react-router-dom'

export function AdminGuard() {
  const { user, isAuthenticated } = useAuth()

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />
  }

  const isAdmin = isAdminRole(user?.role)
  if (!isAdmin) {
    return <Navigate to="/" replace />
  }

  return <Outlet />
}
