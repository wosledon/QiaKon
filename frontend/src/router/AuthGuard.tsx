import { useAuth } from '@/stores/authStore'
import { Navigate, Outlet } from 'react-router-dom'

export function AuthGuard() {
  const { isAuthenticated } = useAuth()

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />
  }

  return <Outlet />
}
