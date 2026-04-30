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

// === Admin Types ===

export interface AdminUser {
  id: string
  username: string
  displayName?: string
  email: string
  departmentId?: string
  departmentName?: string
  role: string
  status: 'active' | 'inactive'
  lastLoginAt?: string
  createdAt: string
}

export interface Role {
  id: string
  name: string
  description?: string
  isSystem: boolean
  userCount: number
  permissions: PermissionMatrix
}

export interface Department {
  id: string
  name: string
  parentId?: string
  parentName?: string
  children?: Department[]
  memberCount?: number
  createdAt?: string
}

export interface PermissionMatrix {
  canReadPublicDocuments: boolean
  canWritePublicDocuments: boolean
  canDeletePublicDocuments: boolean
  canReadDepartmentDocuments: boolean
  canWriteDepartmentDocuments: boolean
  canDeleteDepartmentDocuments: boolean
  canReadAllDocuments: boolean
  canWriteAllDocuments: boolean
  canDeleteAllDocuments: boolean
  canManageUsers: boolean
  canManageRoles: boolean
  canManageDepartments: boolean
  canViewAuditLogs: boolean
  canManageSystemConfig: boolean
}

export interface CreateUserRequest {
  username: string
  email: string
  password: string
  departmentId?: string
  role: string
}

export interface UpdateUserRequest {
  departmentId?: string
  role?: string
  status?: 'active' | 'inactive'
}

export interface CreateRoleRequest {
  name: string
  description?: string
  permissions: PermissionMatrix
}

export interface UpdateRoleRequest {
  name?: string
  description?: string
  permissions?: PermissionMatrix
}

export interface CreateDepartmentRequest {
  name: string
  parentId?: string
}

export interface UpdateDepartmentRequest {
  name?: string
  parentId?: string
}

export interface UserFilters {
  departmentId?: string
  role?: string
  status?: 'active' | 'inactive'
  keyword?: string
  page?: number
  pageSize?: number
}

export interface BatchOperationRequest {
  ids: string[]
  operation: 'enable' | 'disable' | 'delete'
}

// === End Admin Types ===

export interface ChatRequest {
  query: string
  conversationId?: string
  modelId?: string
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
  content?: string
  chunks?: DocumentChunk[]
}

export interface DocumentChunk {
  id: string
  index: number
  content: string
  chunkingStrategy?: string
  summary?: string
  vectorDimension?: number
  status: 'pending' | 'indexing' | 'completed' | 'failed'
}

export interface DocumentDetail extends Document {
  content: string
  chunks: DocumentChunk[]
  parsedAt?: string
}

export interface DocumentListParams {
  page?: number
  pageSize?: number
  status?: string
  department?: string
  search?: string
  sortBy?: string
  sortOrder?: 'asc' | 'desc'
  startDate?: string
  endDate?: string
  [key: string]: unknown
}

export interface DocumentUploadMetadata {
  title?: string
  description?: string
  departmentId?: string
  accessLevel?: 'Public' | 'Department' | 'Restricted' | 'Confidential'
  chunkingStrategy?: 'Auto' | 'MoE' | 'Character'
}

export interface IndexQueueItem {
  documentId: string
  title: string
  status: 'pending' | 'indexing' | 'completed' | 'failed'
  progress: number
  createdAt: string
  updatedAt?: string
  errorMessage?: string
}

export interface IndexStats {
  totalChunks: number
  successRate: number
  avgDuration: number
  pendingCount: number
  indexingCount: number
  completedCount: number
  failedCount: number
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
  createdAt?: string
  properties: Record<string, unknown>
}

export interface GraphRelation {
  id: string
  sourceId: string
  sourceName: string
  targetId: string
  targetName: string
  type: string
  createdAt?: string
  properties?: Record<string, unknown>
}

export interface GraphOverview {
  totalEntities: number
  totalRelations: number
  departmentEntities: number
  publicEntities: number
}

export interface GraphTypeDistribution {
  type: string
  count: number
  percentage: number
}

export interface GraphNeighbor {
  entity: GraphEntity
  relationType: string
  direction: 'out' | 'in'
}

export interface GraphRelationGroup {
  type: string
  count: number
  relations: GraphRelation[]
}

export interface GraphQueryRequest {
  startEntityId?: string
  endEntityId?: string
  relationType?: string
  maxHops?: number
}

export interface GraphQueryPathRequest {
  sourceId: string
  targetId: string
  maxPaths?: number
}

export interface GraphQueryMultiHopRequest {
  startEntityId: string
  maxHops: number
}

export interface GraphQueryNeighborRequest {
  entityId: string
  direction: 'out' | 'in' | 'both'
}

export interface GraphQueryAggregateRequest {
  groupBy: 'type' | 'department'
  filterType?: string
  filterDepartment?: string
}

export interface GraphQueryResponseData {
  paths: GraphPath[]
}

export interface GraphPath {
  nodes: GraphEntity[]
  edges: GraphRelation[]
}

export interface GraphMultiHopResponse {
  entities: GraphEntity[]
}

export interface GraphNeighborResponse {
  neighbors: GraphNeighbor[]
}

export interface GraphAggregateItem {
  key: string
  count: number
}

export interface GraphAggregateResponse {
  results: GraphAggregateItem[]
}

export interface EntitySearchResult {
  id: string
  name: string
  type: string
}

export interface GraphEntityParams {
  page?: number
  pageSize?: number
  search?: string
  type?: string
  department?: string
  [key: string]: unknown
}

export interface GraphRelationParams {
  page?: number
  pageSize?: number
  type?: string
  sourceId?: string
  targetId?: string
  [key: string]: unknown
}

export interface ApiError {
  message: string
  code?: string
  status?: number
}

// === Dashboard Types ===
export interface DashboardStats {
  totalDocuments: number
  totalGraphEntities: number
  todayChats: number
  activeUsers: number
}

export interface RecentDocument {
  id: string
  title: string
  departmentName: string
  createdAt: string
  status: 'pending' | 'indexing' | 'completed' | 'failed'
}

export interface RecentChat {
  id: string
  question: string
  answer: string
  createdAt: string
}

export interface SystemHealth {
  name: string
  status: 'active' | 'warning' | 'error' | 'inactive'
  responseTime?: number
  message?: string
}

// === LLM Models Types ===
export type ProviderType = 'openai' | 'anthropic'
export type ModelType = 'inference' | 'embedding'

export interface LlmProvider {
  id: string
  name: string
  type: ProviderType
  baseUrl: string
  apiKey?: string
  timeout?: number
  retryCount?: number
  modelCount: number
  models: LlmModel[]
}

export interface LlmModel {
  id: string
  providerId: string
  name: string
  type: ModelType
  modelName: string
  dimension?: number
  maxTokens?: number
  isDefault: boolean
  isEnabled: boolean
}

export interface EmbeddingModel {
  name: string
  dimension: number
  status: string
  isBuiltIn: boolean
}

export interface ProviderFormData {
  name: string
  type: ProviderType
  apiKey: string
  baseUrl: string
  timeout?: number
  retryCount?: number
}

export interface ModelFormData {
  type: ModelType
  name: string
  modelName: string
  dimension?: number
  maxTokens?: number
  isDefault?: boolean
}

// === Config Types ===
export interface SystemConfig {
  chunkingStrategy: string
  chunkSize: number
  chunkOverlap: number
  embeddingDimension: number
  cacheLevels: string[]
  cacheTtlSeconds: number
  systemPromptTemplate: string
}

// === Connector Types ===
export type ConnectorType = 'http' | 'npgsql' | 'redis' | 'messageQueue' | 'custom'
export type ConnectorState = 'disconnected' | 'connecting' | 'connected' | 'healthy' | 'unhealthy' | 'closed'

export interface ConnectorEndpoint {
  name: string
  url: string
  method: string
}

export interface Connector {
  id: string
  name: string
  type: ConnectorType
  state: ConnectorState
  baseUrl?: string
  endpoints?: ConnectorEndpoint[]
  connectionString?: string
  commandTimeout?: number
  isHealthy?: boolean
  lastHealthCheck?: string
}

export interface ConnectorFormData {
  name: string
  type: ConnectorType
  baseUrl?: string
  endpoints?: ConnectorEndpoint[]
  connectionString?: string
  commandTimeout?: number
}

// === Health Types ===
export interface ComponentHealth {
  name: string
  status: 'healthy' | 'unhealthy' | 'degraded' | 'unknown'
  responseTime?: number
  error?: string
  details?: Record<string, unknown>
}

// === Audit Types ===
export interface AuditLog {
  id: string
  userId: string
  username: string
  operationType: string
  resourceType: string
  resourceId: string
  result: 'success' | 'failure'
  createdAt: string
  ipAddress?: string
  details?: string
  beforeValue?: string
  afterValue?: string
}

// === Profile Types ===
export interface ProfileUpdateData {
  displayName?: string
  email?: string
  departmentName?: string
}

export interface PasswordChangeData {
  currentPassword: string
  newPassword: string
  confirmPassword: string
}

// === RAG History Types ===
export interface ConversationHistoryDto {
  id: string
  title: string
  messageCount: number
  createdAt: string
  updatedAt: string
}

export interface ConversationDetailDto {
  id: string
  title: string
  messages: { id: string; role: string; content: string; createdAt: string; sources?: unknown[] }[]
  createdAt: string
  updatedAt: string
}

// === Workflow Types ===
export interface StageInfo {
  name: string
  mode: string
  stepCount: number
}

export interface WorkflowDefinition {
  id: string
  name: string
  description: string
  stageCount: number
  stages?: StageInfo[]
  config?: Record<string, unknown>
  createdAt: string
  updatedAt?: string
  isSystem?: boolean
}

export interface WorkflowExecution {
  id: string
  pipelineName: string
  status: string
  startedAt: string
  completedAt: string | null
  duration: number | null
  error: string | null
}

export interface StepResultDetail {
  stepName: string
  status: string
  errorMessage?: string | null
  durationMs: number
  output?: Record<string, unknown> | null
}

export interface StageResultDetail {
  stageName: string
  isSuccess: boolean
  durationMs: number
  stepResults: StepResultDetail[]
}

export interface WorkflowExecutionDetail {
  executionId: string
  pipelineName: string
  status: string
  isSuccess: boolean
  stageResults: StageResultDetail[]
  totalDurationMs: number
  output?: Record<string, unknown> | null
  error?: string | null
}

// === Workflow Orchestration Types ===
export interface WorkflowStepConfig {
  name: string
  type: 'llm' | 'retrieval' | 'graph' | 'transform' | 'condition' | 'custom'
  config?: Record<string, unknown>
}

export interface WorkflowStageConfig {
  name: string
  mode: 'sequential' | 'parallel' | 'fallback'
  steps: WorkflowStepConfig[]
}

export interface WorkflowExecutionInput {
  executionId: string
  pipelineName: string
  input: Record<string, unknown>
  startedAt: string
}

// === Graph Preview Types ===
export interface GraphPreviewNode {
  id: string
  name: string
  type: string
  departmentName?: string
  isPublic?: boolean
  degree?: number
}

export interface GraphPreviewEdge {
  id: string
  sourceId: string
  targetId: string
  type: string
}

export interface GraphPreviewData {
  nodes: GraphPreviewNode[]
  edges: GraphPreviewEdge[]
  totalNodeCount?: number
  totalEdgeCount?: number
}
