import type {
  ApiResponse,
  AuthResponseData,
  ChatRequest,
  ChatResponseData,
  Document,
  GraphEntity,
  GraphQueryRequest,
  GraphQueryResponseData,
  GraphRelation,
  LoginCredentials,
  PagedList,
} from '@/types'

const BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000/api'

function getToken(): string | null {
  return localStorage.getItem('token')
}

function buildHeaders(init?: HeadersInit, includeContentType = true): Headers {
  const headers = new Headers(init)
  if (includeContentType) {
    headers.set('Content-Type', 'application/json')
  }
  const token = getToken()
  if (token) {
    headers.set('Authorization', `Bearer ${token}`)
  }
  return headers
}

async function handleResponse<T>(response: Response): Promise<T> {
  if (!response.ok) {
    let message = `请求失败 (${response.status})`
    let code: string | undefined
    try {
      const body = await response.json()
      message = body.message || body.error || `请求失败 (${response.status})`
      code = body.code
    } catch {
      /* ignore */
    }
    const error = new Error(message) as Error & { code?: string; status?: number }
    error.code = code
    error.status = response.status
    throw error
  }
  if (response.status === 204) {
    return undefined as T
  }
  const result = (await response.json()) as ApiResponse<T>
  if (!result.success) {
    const error = new Error(result.message || '请求失败') as Error & { code?: string; status?: number }
    error.code = result.code
    error.status = response.status
    throw error
  }
  return result.data
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
  const headers = buildHeaders({}, false)
  const response = await fetch(`${BASE_URL}${path}`, {
    method: 'POST',
    headers,
    body: formData,
  })
  return handleResponse<T>(response)
}

// Auth API
export const authApi = {
  login: (credentials: LoginCredentials) =>
    apiPost<AuthResponseData>('/auth/login', credentials),
}

// RAG / Chat API
export const chatApi = {
  send: (request: ChatRequest) =>
    apiPost<ChatResponseData>('/rag/chat', request),
}

// Document API
export const documentApi = {
  list: (page = 1, pageSize = 20) =>
    apiGet<PagedList<Document>>(`/documents?page=${page}&pageSize=${pageSize}`),
  upload: (file: File, metadata?: Record<string, string>) => {
    const formData = new FormData()
    formData.append('file', file)
    if (metadata) {
      Object.entries(metadata).forEach(([k, v]) => formData.append(k, v))
    }
    return apiUpload<Document>('/documents', formData)
  },
  delete: (id: string) => apiDelete<void>(`/documents/${id}`),
}

// Graph API
export const graphApi = {
  entities: () => apiGet<PagedList<GraphEntity>>('/graph/entities'),
  relations: () => apiGet<PagedList<GraphRelation>>('/graph/relations'),
  createEntity: (entity: { name: string; type: string; properties?: Record<string, unknown> }) =>
    apiPost<GraphEntity>('/graph/entities', { ...entity, departmentId: null, accessLevel: 'department' }),
  createRelation: (relation: { sourceId: string; targetId: string; type: string; properties?: Record<string, unknown> }) =>
    apiPost<GraphRelation>('/graph/relations', relation),
  query: (request: GraphQueryRequest) =>
    apiPost<GraphQueryResponseData>('/graph/query', request),
}
