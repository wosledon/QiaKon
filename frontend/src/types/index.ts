export interface User {
  id: string
  username: string
  displayName?: string
}

export interface LoginCredentials {
  username: string
  password: string
}

export interface AuthResponse {
  token: string
  user: User
}

export interface ChatMessage {
  id: string
  role: 'user' | 'assistant'
  content: string
  timestamp: string
  sources?: Source[]
}

export interface Source {
  id: string
  title: string
  snippet: string
  score: number
}

export interface Document {
  id: string
  name: string
  size: number
  type: string
  status: 'pending' | 'processing' | 'completed' | 'failed'
  createdAt: string
  updatedAt: string
}

export interface GraphEntity {
  id: string
  name: string
  type: string
  properties: Record<string, unknown>
}

export interface GraphRelation {
  id: string
  source: string
  target: string
  type: string
}

export interface ApiError {
  message: string
  code?: string
  status?: number
}
