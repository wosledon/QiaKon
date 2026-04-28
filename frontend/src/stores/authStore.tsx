import { createContext, useContext, useState, useCallback, type ReactNode } from 'react'
import type { User, AuthResponseData } from '@/types'

interface AuthState {
  user: User | null
  token: string | null
  expiresIn: number | null
  isAuthenticated: boolean
}

interface AuthContextValue extends AuthState {
  login: (data: AuthResponseData) => void
  logout: () => void
}

function loadInitialState(): AuthState {
  try {
    const token = localStorage.getItem('token')
    const userJson = localStorage.getItem('user')
    if (token && userJson) {
      const user = JSON.parse(userJson) as User
      const expiresInRaw = localStorage.getItem('expiresIn')
      const expiresIn = expiresInRaw ? Number(expiresInRaw) : null
      return { user, token, expiresIn, isAuthenticated: true }
    }
  } catch {
    localStorage.removeItem('token')
    localStorage.removeItem('user')
    localStorage.removeItem('expiresIn')
  }
  return { user: null, token: null, expiresIn: null, isAuthenticated: false }
}

const AuthContext = createContext<AuthContextValue | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<AuthState>(loadInitialState)

  const login = useCallback((data: AuthResponseData) => {
    localStorage.setItem('token', data.token)
    if (data.expiresIn != null) {
      localStorage.setItem('expiresIn', String(data.expiresIn))
    }
    localStorage.setItem('user', JSON.stringify(data.user))
    setState({
      user: data.user,
      token: data.token,
      expiresIn: data.expiresIn ?? null,
      isAuthenticated: true,
    })
  }, [])

  const logout = useCallback(() => {
    localStorage.removeItem('token')
    localStorage.removeItem('expiresIn')
    localStorage.removeItem('user')
    setState({ user: null, token: null, expiresIn: null, isAuthenticated: false })
  }, [])

  return (
    <AuthContext.Provider value={{ ...state, login, logout }}>
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext)
  if (!ctx) {
    throw new Error('useAuth must be used within AuthProvider')
  }
  return ctx
}
