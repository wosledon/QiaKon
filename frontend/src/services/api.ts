import type {
  AdminUser,
  ApiResponse,
  AuditLog,
  AuthResponseData,
  BatchOperationRequest,
  ChatRequest,
  ChatResponseData,
  ComponentHealth,
  Connector,
  ConnectorFormData,
  CreateDepartmentRequest,
  CreateRoleRequest,
  CreateUserRequest,
  DashboardStats,
  Department,
  Document,
  DocumentDetail,
  DocumentListParams,
  EmbeddingModel,
  IndexQueueItem,
  IndexStats,
  GraphAggregateResponse,
  GraphEntity,
  GraphEntityParams,
  GraphMultiHopResponse,
  GraphNeighborResponse,
  GraphOverview,
  GraphQueryAggregateRequest,
  GraphQueryMultiHopRequest,
  GraphQueryNeighborRequest,
  GraphQueryPathRequest,
  GraphQueryResponseData,
  GraphRelation,
  GraphRelationParams,
  GraphTypeDistribution,
  LoginCredentials,
  LlmModel,
  LlmProvider,
  ModelFormData,
  PagedList,
  PermissionMatrix,
  ProfileUpdateData,
  ProviderFormData,
  RecentChat,
  RecentDocument,
  Role,
  SystemConfig,
  SystemHealth,
  UpdateDepartmentRequest,
  UpdateRoleRequest,
  UpdateUserRequest,
  User,
  UserFilters,
} from '@/types'
import { clearAuthStorage } from '@/stores/authStore'

const BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:8080/api'

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
    if (response.status === 401) {
      clearAuthStorage()
      if (typeof window !== 'undefined' && window.location.pathname !== '/login') {
        window.location.replace('/login')
      }
    }
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

export async function apiPut<T>(path: string, body?: unknown): Promise<T> {
  const response = await fetch(`${BASE_URL}${path}`, {
    method: 'PUT',
    headers: buildHeaders(),
    body: body ? JSON.stringify(body) : undefined,
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
    apiPost<ChatResponseData>('/retrieval/chat', request),
}

// Document API
export const documentApi = {
  list: (params?: DocumentListParams) => {
    const query = buildQuery(params ?? {})
    return apiGet<PagedList<Document>>(`/documents${query}`)
  },
  get: (id: string) => apiGet<DocumentDetail>(`/documents/${id}`),
  update: (id: string, data: Partial<Document>) => apiPut<Document>(`/documents/${id}`, data),
  upload: (file: File, metadata?: Record<string, string>) => {
    const formData = new FormData()
    formData.append('file', file)
    if (metadata) {
      Object.entries(metadata).forEach(([k, v]) => formData.append(k, v))
    }
    return apiUpload<Document>('/documents', formData)
  },
  delete: (id: string) => apiDelete<void>(`/documents/${id}`),
  batchDelete: (ids: string[]) => apiPost<void>('/documents/batch-delete', { ids }),
  reparse: (id: string) => apiPost<void>(`/documents/${id}/reparse`, {}),
  reindex: (id: string) => apiPost<void>(`/documents/${id}/reindex`, {}),
  indexQueue: () => apiGet<IndexQueueItem[]>('/documents/index/queue'),
  indexStats: () => apiGet<IndexStats>('/documents/index/stats'),
  retryFailed: () => apiPost<void>('/documents/index/retry-failed', {}),
  rebuildIndex: () => apiPost<void>('/documents/index/rebuild', {}),
}

// Graph API
function buildQuery(params: Record<string, unknown>): string {
  const qs = Object.entries(params)
    .filter(([, v]) => v !== undefined && v !== '')
    .map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(String(v))}`)
    .join('&')
  return qs ? `?${qs}` : ''
}

export const graphApi = {
  overview: () => apiGet<GraphOverview>('/graphs'),
  entityTypes: () => apiGet<GraphTypeDistribution[]>('/graphs/stats/entity-types'),
  relationTypes: () => apiGet<GraphTypeDistribution[]>('/graphs/stats/relation-types'),

  entities: (params?: GraphEntityParams) =>
    apiGet<PagedList<GraphEntity>>(`/graphs/entities${buildQuery(params ?? {})}`),
  entity: (id: string) => apiGet<GraphEntity>(`/graphs/entities/${id}`),
  createEntity: (entity: { name: string; type: string; properties?: Record<string, unknown>; departmentId?: string | null; isPublic?: boolean }) =>
    apiPost<GraphEntity>('/graphs/entities', entity),
  updateEntity: (id: string, entity: { name: string; type: string; properties?: Record<string, unknown>; departmentId?: string | null; isPublic?: boolean }) =>
    apiPut<GraphEntity>(`/graphs/entities/${id}`, entity),
  deleteEntity: (id: string) => apiDelete<void>(`/graphs/entities/${id}`),
  searchEntities: (keyword: string) =>
    apiGet<GraphEntity[]>(`/graphs/entities/search?keyword=${encodeURIComponent(keyword)}`),

  relations: (params?: GraphRelationParams) =>
    apiGet<PagedList<GraphRelation>>(`/graphs/relations${buildQuery(params ?? {})}`),
  relation: (id: string) => apiGet<GraphRelation>(`/graphs/relations/${id}`),
  createRelation: (relation: { sourceId: string; targetId: string; type: string; properties?: Record<string, unknown> }) =>
    apiPost<GraphRelation>('/graphs/relations', relation),
  updateRelation: (id: string, relation: { sourceId: string; targetId: string; type: string; properties?: Record<string, unknown> }) =>
    apiPut<GraphRelation>(`/graphs/relations/${id}`, relation),
  deleteRelation: (id: string) => apiDelete<void>(`/graphs/relations/${id}`),

  queryPath: (request: GraphQueryPathRequest) =>
    apiPost<GraphQueryResponseData>('/graphs/query/path', request),
  queryMultiHop: (request: GraphQueryMultiHopRequest) =>
    apiPost<GraphMultiHopResponse>('/graphs/query/multi-hop', request),
  queryNeighbors: (request: GraphQueryNeighborRequest) =>
    apiPost<GraphNeighborResponse>('/graphs/query/neighbors', request),
  queryAggregate: (request: GraphQueryAggregateRequest) =>
    apiPost<GraphAggregateResponse>('/graphs/query/aggregate', request),
}

// Admin API
export const adminApi = {
  users: {
    list: (filters?: UserFilters) => {
      const params = new URLSearchParams()
      if (filters) {
        if (filters.departmentId) params.set('departmentId', filters.departmentId)
        if (filters.role) params.set('role', filters.role)
        if (filters.status) params.set('status', filters.status)
        if (filters.keyword) params.set('keyword', filters.keyword)
        if (filters.page) params.set('page', String(filters.page))
        if (filters.pageSize) params.set('pageSize', String(filters.pageSize))
      }
      const query = params.toString()
      return apiGet<PagedList<AdminUser>>(`/admin/users${query ? '?' + query : ''}`)
    },
    create: (data: CreateUserRequest) => apiPost<AdminUser>('/admin/users', data),
    update: (id: string, data: UpdateUserRequest) => apiPut<AdminUser>(`/admin/users/${id}`, data),
    resetPassword: (id: string) => apiPut<void>(`/admin/users/${id}/reset-password`, {}),
    updateStatus: (id: string, status: 'active' | 'inactive') => apiPut<void>(`/admin/users/${id}/status`, { status }),
    batch: (data: BatchOperationRequest) => apiPost<void>('/admin/users/batch', data),
    delete: (id: string) => apiDelete<void>(`/admin/users/${id}`),
  },
  roles: {
    list: () => apiGet<Role[]>('/admin/roles'),
    create: (data: CreateRoleRequest) => apiPost<Role>('/admin/roles', data),
    update: (id: string, data: UpdateRoleRequest) => apiPut<Role>(`/admin/roles/${id}`, data),
    delete: (id: string) => apiDelete<void>(`/admin/roles/${id}`),
    updatePermissions: (id: string, permissions: PermissionMatrix) => apiPut<void>(`/admin/roles/${id}/permissions`, permissions),
  },
  departments: {
    list: () => apiGet<Department[]>('/admin/departments'),
    create: (data: CreateDepartmentRequest) => apiPost<Department>('/admin/departments', data),
    update: (id: string, data: UpdateDepartmentRequest) => apiPut<Department>(`/admin/departments/${id}`, data),
    delete: (id: string) => apiDelete<void>(`/admin/departments/${id}`),
    members: (id: string) => apiGet<AdminUser[]>(`/admin/departments/${id}/members`),
  },
}

// Dashboard API
export const dashboardApi = {
  stats: () => apiGet<DashboardStats>('/dashboard/stats'),
  recentDocuments: () => apiGet<RecentDocument[]>('/dashboard/recent-documents'),
  recentChats: () => apiGet<RecentChat[]>('/dashboard/recent-chats'),
  health: () => apiGet<SystemHealth[]>('/dashboard/health'),
}

// LLM Models API
export const llmModelsApi = {
  providers: () => apiGet<LlmProvider[]>('/admin/llm-models/providers'),
  addProvider: (data: ProviderFormData) => apiPost<LlmProvider>('/admin/llm-models/providers', data),
  updateProvider: (id: string, data: ProviderFormData) => apiPut<LlmProvider>(`/admin/llm-models/providers/${id}`, data),
  deleteProvider: (id: string) => apiDelete<void>(`/admin/llm-models/providers/${id}`),
  testProvider: (id: string) => apiPost<{ success: boolean; message?: string }>(`/admin/llm-models/providers/${id}/test`, {}),
  providerModels: (id: string) => apiGet<LlmModel[]>(`/admin/llm-models/providers/${id}/models`),
  addModel: (data: ModelFormData & { providerId: string }) => apiPost<LlmModel>('/admin/llm-models/models', data),
  updateModel: (id: string, data: Partial<ModelFormData>) => apiPut<LlmModel>(`/admin/llm-models/models/${id}`, data),
  deleteModel: (id: string) => apiDelete<void>(`/admin/llm-models/models/${id}`),
  setDefaultModel: (id: string) => apiPut<void>(`/admin/llm-models/models/${id}/set-default`, {}),
  toggleModel: (id: string) => apiPut<void>(`/admin/llm-models/models/${id}/toggle`, {}),
  embeddings: () => apiGet<EmbeddingModel[]>('/admin/llm-models/embeddings'),
}

// Config API
export const configApi = {
  get: () => apiGet<SystemConfig>('/admin/config'),
  update: (data: Partial<SystemConfig>) => apiPut<SystemConfig>('/admin/config', data),
  reset: () => apiPost<SystemConfig>('/admin/config/reset', {}),
}

// Connectors API
export const connectorsApi = {
  list: () => apiGet<Connector[]>('/admin/connectors'),
  add: (data: ConnectorFormData) => apiPost<Connector>('/admin/connectors', data),
  update: (id: string, data: ConnectorFormData) => apiPut<Connector>(`/admin/connectors/${id}`, data),
  delete: (id: string) => apiDelete<void>(`/admin/connectors/${id}`),
  health: (id: string) => apiPost<{ isHealthy: boolean; message?: string }>(`/admin/connectors/${id}/health`, {}),
}

interface HealthOverviewDto {
  overallStatus: string
  checkedAt: string
  components: Record<string, { status: string; responseTimeMs: number; message?: string | null }>
}

function normalizeHealthStatus(status?: string): ComponentHealth['status'] {
  switch (status?.toLowerCase()) {
    case 'healthy':
      return 'healthy'
    case 'degraded':
      return 'degraded'
    case 'unhealthy':
      return 'unhealthy'
    default:
      return 'unknown'
  }
}

// Health API
export const healthApi = {
  overview: async () => {
    try {
      const data = await apiGet<HealthOverviewDto>('/health')
      return Object.entries(data.components ?? {}).map(([name, component]) => ({
        name,
        status: normalizeHealthStatus(component.status),
        responseTime: component.responseTimeMs,
        error: component.message ?? undefined,
        details: {
          overallStatus: data.overallStatus,
          checkedAt: data.checkedAt,
        },
      })) satisfies ComponentHealth[]
    } catch {
      const fallback = await apiGet<SystemHealth[]>('/dashboard/health')
      return fallback.map((component) => ({
        name: component.name,
        status:
          component.status === 'active'
            ? 'healthy'
            : component.status === 'warning'
            ? 'degraded'
            : component.status === 'error'
            ? 'unhealthy'
            : 'unknown',
        responseTime: component.responseTime,
        error: component.message,
      })) satisfies ComponentHealth[]
    }
  },
  component: (name: string) => apiGet<ComponentHealth>(`/health/${name}`),
}

// Audit API
export const auditApi = {
  logs: (page = 1, pageSize = 20, filters?: { operationType?: string; username?: string; startDate?: string; endDate?: string }) => {
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
    if (filters?.operationType) params.append('operationType', filters.operationType)
    if (filters?.username) params.append('username', filters.username)
    if (filters?.startDate) params.append('startDate', filters.startDate)
    if (filters?.endDate) params.append('endDate', filters.endDate)
    return apiGet<PagedList<AuditLog>>(`/admin/audit/logs?${params.toString()}`)
  },
}

// Profile API
export const profileApi = {
  get: () => apiGet<User>('/profile'),
  update: (data: ProfileUpdateData) => apiPut<User>('/profile', data),
  changePassword: (data: { currentPassword: string; newPassword: string }) => apiPut<void>('/profile/password', data),
  logout: () => apiPost<void>('/profile/logout', {}),
}
