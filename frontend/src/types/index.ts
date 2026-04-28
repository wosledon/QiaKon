export interface ApiResponse<T> {
  success: boolean
  message?: string
  data: T
  code?: string
}

export interface User {
  id: string
  username: string
  displayName?: string
  email?: string
  role?: string
  departmentId?: string
  departmentName?: string
}

export interface LoginCredentials {
  username: string
  password: string
}

export interface AuthResponseData {
  token: string
  expiresIn?: number
  user: User
}

export interface ChatRequest {
  query: string
  conversationId?: string
  retrievalOptions?: Record<string, unknown>
}

export interface ChatResponseData {
  response: string
  sources: Source[]
  conversationId: string
  turns: number
}

export interface Source {
  documentId: string
  title: string
  text: string
  snippet: string
  score: number
}

export interface Document {
  id: string
  title: string
  type: string
  departmentId: string
  departmentName: string
  accessLevel: string
  indexStatus: 'pending' | 'indexing' | 'completed' | 'failed'
  version: number
  createdAt: string
  updatedAt?: string
  size: number
  metadata?: Record<string, unknown>
}

export interface PagedList<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}

export interface GraphEntity {
  id: string
  name: string
  type: string
  departmentId?: string
  departmentName?: string
  isPublic?: boolean
  properties: Record<string, unknown>
}

export interface GraphRelation {
  id: string
  sourceId: string
  sourceName: string
  targetId: string
  targetName: string
  type: string
  properties?: Record<string, unknown>
}

export interface GraphQueryRequest {
  startEntityId?: string
  endEntityId?: string
  relationType?: string
  maxHops?: number
}

export interface GraphQueryResponseData {
  paths: GraphPath[]
}

export interface GraphPath {
  nodes: GraphEntity[]
  edges: GraphRelation[]
}

export interface ApiError {
  message: string
  code?: string
  status?: number
}
