import { useAuth } from '@/stores/authStore'
import { Navigate } from 'react-router-dom'
import type { ReactNode } from 'react'

export function LoginRedirect({ children }: { children: ReactNode }) {
  const { isAuthenticated } = useAuth()

  if (isAuthenticated) {
    return <Navigate to="/" replace />
  }

  return <>{children}</>
}
