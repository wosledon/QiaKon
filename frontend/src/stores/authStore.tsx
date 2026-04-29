import { createContext, useContext, useState, useCallback, type ReactNode } from 'react'
import type { User, AuthResponseData } from '@/types'

const TOKEN_KEY = 'token'
const USER_KEY = 'user'
const EXPIRES_IN_KEY = 'expiresIn'
const TOKEN_EXPIRES_AT_KEY = 'tokenExpiresAt'

export function isAdminRole(role?: string | null): boolean {
  const normalizedRole = role?.trim().toLowerCase()
  return normalizedRole === 'admin' || normalizedRole === 'administrator'
}

function clearStoredAuth(): void {
  localStorage.removeItem(TOKEN_KEY)
  localStorage.removeItem(USER_KEY)
  localStorage.removeItem(EXPIRES_IN_KEY)
  localStorage.removeItem(TOKEN_EXPIRES_AT_KEY)
}

function decodeJwtExpiration(token: string): number | null {
  try {
    const payload = token.split('.')[1]
    if (!payload) return null
    const normalized = payload.replace(/-/g, '+').replace(/_/g, '/')
    const padded = normalized.padEnd(Math.ceil(normalized.length / 4) * 4, '=')
    const decoded = JSON.parse(atob(padded)) as { exp?: number }
    return typeof decoded.exp === 'number' ? decoded.exp : null
  } catch {
    return null
  }
}

function resolveExpiresAt(token: string, expiresIn?: number | null): number | null {
  return decodeJwtExpiration(token) ?? (typeof expiresIn === 'number' ? Math.floor(Date.now() / 1000) + expiresIn : null)
}

export function clearAuthStorage(): void {
  clearStoredAuth()
}

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
    const token = localStorage.getItem(TOKEN_KEY)
    const userJson = localStorage.getItem(USER_KEY)
    if (token && userJson) {
      const user = JSON.parse(userJson) as User
      const expiresInRaw = localStorage.getItem(EXPIRES_IN_KEY)
      const expiresIn = expiresInRaw ? Number(expiresInRaw) : null
      const expiresAtRaw = localStorage.getItem(TOKEN_EXPIRES_AT_KEY)
      const expiresAt = expiresAtRaw ? Number(expiresAtRaw) : resolveExpiresAt(token)
      if (expiresAt != null && expiresAt <= Math.floor(Date.now() / 1000)) {
        clearStoredAuth()
        return { user: null, token: null, expiresIn: null, isAuthenticated: false }
      }
      return { user, token, expiresIn, isAuthenticated: true }
    }
  } catch {
    clearStoredAuth()
  }
  return { user: null, token: null, expiresIn: null, isAuthenticated: false }
}

const AuthContext = createContext<AuthContextValue | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<AuthState>(loadInitialState)

  const login = useCallback((data: AuthResponseData) => {
    const expiresAt = resolveExpiresAt(data.token, data.expiresIn)

    localStorage.setItem(TOKEN_KEY, data.token)
    if (data.expiresIn != null) {
      localStorage.setItem(EXPIRES_IN_KEY, String(data.expiresIn))
    }
    if (expiresAt != null) {
      localStorage.setItem(TOKEN_EXPIRES_AT_KEY, String(expiresAt))
    }
    localStorage.setItem(USER_KEY, JSON.stringify(data.user))
    setState({
      user: data.user,
      token: data.token,
      expiresIn: data.expiresIn ?? null,
      isAuthenticated: true,
    })
  }, [])

  const logout = useCallback(() => {
    clearStoredAuth()
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
