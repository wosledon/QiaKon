import type { ApiError, AuthResponse, ChatMessage, Document, GraphEntity, LoginCredentials } from '@/types'

const BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000/api'

function getToken(): string | null {
  return localStorage.getItem('token')
}

function buildHeaders(init?: HeadersInit): Headers {
  const headers = new Headers(init)
  headers.set('Content-Type', 'application/json')
  const token = getToken()
  if (token) {
    headers.set('Authorization', `Bearer ${token}`)
  }
  return headers
}

async function handleResponse<T>(response: Response): Promise<T> {
  if (!response.ok) {
    let error: ApiError
    try {
      const body = await response.json()
      error = { message: body.message || `请求失败 (${response.status})`, code: body.code, status: response.status }
    } catch {
      error = { message: `请求失败 (${response.status})`, status: response.status }
    }
    throw error
  }
  if (response.status === 204) {
    return undefined as T
  }
  return response.json() as Promise<T>
}

export async function apiGet<T>(path: string): Promise<T> {
  const response = await fetch(`${BASE_URL}${path}`, {
    method: 'GET',
    headers: buildHeaders(),
  })
  return handleResponse<T>(response)
}

export async function apiPost<T>(path: string, body: unknown): Promise<T> {
  const response = await fetch(`${BASE_URL}${path}`, {
    method: 'POST',
    headers: buildHeaders(),
    body: JSON.stringify(body),
  })
  return handleResponse<T>(response)
}

export async function apiDelete<T>(path: string): Promise<T> {
  const response = await fetch(`${BASE_URL}${path}`, {
    method: 'DELETE',
    headers: buildHeaders(),
  })
  return handleResponse<T>(response)
}

export async function apiUpload<T>(path: string, formData: FormData): Promise<T> {
  const headers = new Headers()
  const token = getToken()
  if (token) {
    headers.set('Authorization', `Bearer ${token}`)
  }
  const response = await fetch(`${BASE_URL}${path}`, {
    method: 'POST',
    headers,
    body: formData,
  })
  return handleResponse<T>(response)
}

export async function* streamChat(question: string): AsyncGenerator<string, void, unknown> {
  const response = await fetch(`${BASE_URL}/chat/stream`, {
    method: 'POST',
    headers: buildHeaders(),
    body: JSON.stringify({ question }),
  })

  if (!response.ok) {
    let error: ApiError
    try {
      const body = await response.json()
      error = { message: body.message || `请求失败 (${response.status})`, code: body.code, status: response.status }
    } catch {
      error = { message: `请求失败 (${response.status})`, status: response.status }
    }
    throw error
  }

  const reader = response.body?.getReader()
  if (!reader) return

  const decoder = new TextDecoder()
  let buffer = ''

  while (true) {
    const { done, value } = await reader.read()
    if (done) break
    buffer += decoder.decode(value, { stream: true })
    const lines = buffer.split('\n')
    buffer = lines.pop() || ''
    for (const line of lines) {
      const trimmed = line.trim()
      if (trimmed.startsWith('data: ')) {
        const data = trimmed.slice(6)
        if (data === '[DONE]') return
        yield data
      }
    }
  }
}

// Auth API
export const authApi = {
  login: (credentials: LoginCredentials) => apiPost<AuthResponse>('/auth/login', credentials),
}

// Chat API
export const chatApi = {
  send: (question: string) => streamChat(question),
  history: () => apiGet<ChatMessage[]>('/chat/history'),
}

// Document API
export const documentApi = {
  list: () => apiGet<Document[]>('/documents'),
  upload: (file: File) => {
    const formData = new FormData()
    formData.append('file', file)
    return apiUpload<Document>('/documents', formData)
  },
  delete: (id: string) => apiDelete<void>(`/documents/${id}`),
}

// Graph API
export const graphApi = {
  entities: () => apiGet<GraphEntity[]>('/graph/entities'),
  relations: () => apiGet<GraphEntity[]>('/graph/relations'),
}
