import { createBrowserRouter, Navigate, type RouteObject } from 'react-router-dom'
import { AppLayout } from '@/components/layout/AppLayout'
import { LoginPage } from '@/pages/LoginPage'
import { ChatPage } from '@/pages/ChatPage'
import { DocumentsListPage } from '@/pages/documents/DocumentsListPage'
import { UploadPage } from '@/pages/documents/UploadPage'
import { DetailPage } from '@/pages/documents/DetailPage'
import { EditPage } from '@/pages/documents/EditPage'
import { IndexPage } from '@/pages/documents/IndexPage'
import { DashboardPage } from '@/pages/DashboardPage'
import { GraphOverviewPage } from '@/pages/graphs/GraphOverviewPage'
import { GraphEntitiesPage } from '@/pages/graphs/GraphEntitiesPage'
import { GraphEntityDetailPage } from '@/pages/graphs/GraphEntityDetailPage'
import { GraphRelationsPage } from '@/pages/graphs/GraphRelationsPage'
import { GraphQueryPage } from '@/pages/graphs/GraphQueryPage'
import { ChatHistoryPage } from '@/pages/retrieval/ChatHistoryPage'
import { ChatHistoryDetailPage } from '@/pages/retrieval/ChatHistoryDetailPage'
import { WorkflowsPage } from '@/pages/workflows/WorkflowsPage'
import { WorkflowRunsPage } from '@/pages/workflows/WorkflowRunsPage'
import { AdminDepartmentsPage } from '@/pages/admin/DepartmentsPage'
import { AdminRolesPage } from '@/pages/admin/RolesPage'
import { AdminUsersPage } from '@/pages/admin/UsersPage'
import { AdminLlmModelsPage } from '@/pages/admin/AdminLlmModelsPage'
import { AdminConfigPage } from '@/pages/admin/AdminConfigPage'
import { AdminConnectorsPage } from '@/pages/admin/AdminConnectorsPage'
import { AdminAuditPage } from '@/pages/admin/AdminAuditPage'
import { AdminHealthPage } from '@/pages/admin/AdminHealthPage'
import { ProfilePage } from '@/pages/ProfilePage'
import { AuthGuard } from './AuthGuard'
import { AdminGuard } from './AdminGuard'
import { LoginRedirect } from './LoginRedirect'

const protectedRoutes: RouteObject[] = [
  {
    element: <AppLayout />,
    children: [
      { path: '/', element: <ChatPage /> },
      { path: '/dashboard', element: <DashboardPage /> },
      { path: '/documents', element: <DocumentsListPage /> },
      { path: '/documents/upload', element: <UploadPage /> },
      { path: '/documents/:id', element: <DetailPage /> },
      { path: '/documents/:id/edit', element: <EditPage /> },
      { path: '/documents/index', element: <IndexPage /> },
      { path: '/graphs', element: <GraphOverviewPage /> },
      { path: '/graphs/entities', element: <GraphEntitiesPage /> },
      { path: '/graphs/entities/:id', element: <GraphEntityDetailPage /> },
      { path: '/graphs/relations', element: <GraphRelationsPage /> },
      { path: '/graphs/query', element: <GraphQueryPage /> },
      { path: '/retrieval/chat', element: <ChatPage /> },
      { path: '/retrieval/history', element: <ChatHistoryPage /> },
      { path: '/retrieval/history/:id', element: <ChatHistoryDetailPage /> },
      { path: '/workflows', element: <WorkflowsPage /> },
      { path: '/workflows/runs', element: <WorkflowRunsPage /> },
      {
        element: <AdminGuard />,
        children: [
          { path: '/admin/departments', element: <AdminDepartmentsPage /> },
          { path: '/admin/roles', element: <AdminRolesPage /> },
          { path: '/admin/users', element: <AdminUsersPage /> },
          { path: '/admin/llm-models', element: <AdminLlmModelsPage /> },
          { path: '/admin/config', element: <AdminConfigPage /> },
          { path: '/admin/connectors', element: <AdminConnectorsPage /> },
          { path: '/admin/audit', element: <AdminAuditPage /> },
          { path: '/admin/health', element: <AdminHealthPage /> },
        ],
      },
      { path: '/profile', element: <ProfilePage /> },
    ],
  },
]

export const router = createBrowserRouter([
  {
    path: '/login',
    element: (
      <LoginRedirect>
        <LoginPage />
      </LoginRedirect>
    ),
  },
  {
    element: <AuthGuard />,
    children: protectedRoutes,
  },
  {
    path: '*',
    element: <Navigate to="/" replace />,
  },
])
