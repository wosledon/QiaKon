# QiaKon Frontend - AGENTS.md

> **模块**: 前端 Web 应用  
> **职责**: 用户界面、交互体验、API 集成  
> **技术栈**: React 19 + Vite 7 + TypeScript 5.8 + Tailwind CSS + Material Design 3
> **依赖**: `QiaKon.Api` (HTTP API)

---

## 一、模块职责

本模块是平台的用户界面，提供知识文档管理、知识图谱可视化、检索问答、工作流编排等功能的 Web 交互界面。

**核心职责**:
- 用户认证与权限管理
- 文档管理界面（上传、查看、搜索）
- 知识图谱可视化
- 检索问答界面
- 工作流配置与监控
- 系统管理界面

---

## 二、技术栈

### 2.1 核心技术

| 技术           | 版本   | 用途      |
| -------------- | ------ | --------- |
| React          | 19     | UI 框架   |
| Vite           | 7      | 构建工具  |
| TypeScript     | 5.8    | 类型安全  |
| Tailwind CSS   | 4      | 样式框架  |
| shadcn/ui      | latest | UI 组件库 |
| React Router   | 7      | 路由管理  |
| TanStack Query | 5      | 数据获取  |
| Zustand        | 5      | 状态管理  |

### 2.2 项目结构

```
frontend/
├── public/              # 静态资源
├── src/
│   ├── components/      # 可复用组件
│   │   ├── ui/          # shadcn/ui 组件
│   │   ├── layout/      # 布局组件
│   │   ├── forms/       # 表单组件
│   │   └── charts/      # 图表组件
│   ├── pages/           # 页面组件
│   │   ├── auth/        # 认证页面
│   │   ├── documents/   # 文档管理
│   │   ├── graphs/      # 知识图谱
│   │   ├── retrieval/   # 检索问答
│   │   ├── workflows/   # 工作流
│   │   └── admin/       # 系统管理
│   ├── hooks/           # 自定义 Hooks
│   ├── stores/          # Zustand 状态管理
│   ├── services/        # API 服务层
│   ├── types/           # TypeScript 类型定义
│   ├── utils/           # 工具函数
│   └── assets/          # 图片、字体等
├── index.html
├── vite.config.ts
├── tailwind.config.ts
└── tsconfig.json
```

---

## 三、开发规范

### 3.1 组件规范

#### 3.1.1 组件结构

```tsx
// DocumentCard.tsx
import { Card, CardHeader, CardTitle } from "@/components/ui/card";
import { Document } from "@/types/document";

interface DocumentCardProps {
  document: Document;
  onClick: (id: string) => void;
}

export function DocumentCard({ document, onClick }: DocumentCardProps) {
  return (
    <Card 
      className="cursor-pointer hover:shadow-lg transition-shadow"
      onClick={() => onClick(document.id)}
    >
      <CardHeader>
        <CardTitle>{document.title}</CardTitle>
      </CardHeader>
    </Card>
  );
}
```

#### 3.1.2 组件命名

- 组件文件：PascalCase（如 `DocumentCard.tsx`）
- 组件函数：PascalCase（如 `DocumentCard`）
- Hook 函数：camelCase，`use` 前缀（如 `useDocuments`）

### 3.2 类型定义

```typescript
// types/document.ts
export interface Document {
  id: string;
  title: string;
  content: string;
  departmentId: string;
  isPublic: boolean;
  accessLevel: AccessLevel;
  createdAt: string;
  updatedAt: string;
}

export enum AccessLevel {
  Public = "public",
  Internal = "internal",
  Confidential = "confidential",
  Secret = "secret"
}

export interface DocumentListResponse {
  items: Document[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}
```

### 3.3 API 服务层

```typescript
// services/api/documents.ts
import { apiClient } from "./client";
import { Document, DocumentListResponse } from "@/types/document";

export const documentApi = {
  list: (params: { page: number; pageSize: number }) =>
    apiClient.get<DocumentListResponse>("/documents", { params }),
  
  get: (id: string) =>
    apiClient.get<Document>(`/documents/${id}`),
  
  upload: (formData: FormData) =>
    apiClient.post<Document>("/documents", formData, {
      headers: { "Content-Type": "multipart/form-data" },
    }),
  
  delete: (id: string) =>
    apiClient.delete(`/documents/${id}`),
};
```

### 3.4 数据获取

```typescript
// hooks/useDocuments.ts
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { documentApi } from "@/services/api/documents";

export function useDocuments(page: number, pageSize: number) {
  return useQuery({
    queryKey: ["documents", page, pageSize],
    queryFn: () => documentApi.list({ page, pageSize }),
  });
}

export function useDeleteDocument() {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: documentApi.delete,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["documents"] });
    },
  });
}
```

---

## 四、状态管理

### 4.1 Zustand Store

```typescript
// stores/authStore.ts
import { create } from "zustand";
import { persist } from "zustand/middleware";

interface AuthState {
  user: User | null;
  token: string | null;
  isAuthenticated: boolean;
  login: (user: User, token: string) => void;
  logout: () => void;
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      user: null,
      token: null,
      isAuthenticated: false,
      
      login: (user, token) => set({ 
        user, 
        token, 
        isAuthenticated: true 
      }),
      
      logout: () => set({ 
        user: null, 
        token: null, 
        isAuthenticated: false 
      }),
    }),
    { name: "auth-storage" }
  )
);
```

### 4.2 状态管理原则

- **全局状态**: 用户认证、主题、语言等使用 Zustand
- **服务端状态**: 使用 TanStack Query 管理
- **本地状态**: 组件内部状态使用 `useState`
- **表单状态**: 使用 `react-hook-form`

---

## 五、路由配置

```typescript
// router/index.tsx
import { createBrowserRouter } from "react-router-dom";
import { AuthLayout } from "@/components/layout/AuthLayout";
import { MainLayout } from "@/components/layout/MainLayout";

export const router = createBrowserRouter([
  {
    path: "/auth",
    element: <AuthLayout />,
    children: [
      { path: "login", element: <LoginPage /> },
    ],
  },
  {
    path: "/",
    element: <MainLayout />,
    children: [
      { index: true, element: <DashboardPage /> },
      { path: "documents", element: <DocumentsPage /> },
      { path: "documents/:id", element: <DocumentDetailPage /> },
      { path: "graphs", element: <GraphsPage /> },
      { path: "chat", element: <ChatPage /> },
      { path: "admin", element: <AdminPage /> },
    ],
  },
]);
```

---

## 六、样式规范

### 6.1 Tailwind CSS

- 使用 Tailwind 工具类，避免自定义 CSS
- 响应式设计：`sm:`, `md:`, `lg:`, `xl:` 断点
- 暗色模式：使用 `dark:` 前缀

### 6.2 shadcn/ui 组件

```bash
# 添加组件
npx shadcn@latest add button
npx shadcn@latest add dialog
npx shadcn@latest add table
```

### 6.3 主题配置

```typescript
// tailwind.config.ts
export default {
  theme: {
    extend: {
      colors: {
        primary: {
          50: "#f0f9ff",
          500: "#0ea5e9",
          900: "#0c4a6e",
        },
      },
    },
  },
  plugins: [require("tailwindcss-animate")],
} satisfies Config;
```

---

## 七、认证与权限

### 7.1 路由守卫

```typescript
// components/auth/ProtectedRoute.tsx
import { Navigate } from "react-router-dom";
import { useAuthStore } from "@/stores/authStore";

export function ProtectedRoute({ children }: { children: React.ReactNode }) {
  const isAuthenticated = useAuthStore(state => state.isAuthenticated);
  
  if (!isAuthenticated) {
    return <Navigate to="/auth/login" replace />;
  }
  
  return <>{children}</>;
}
```

### 7.2 权限检查

```typescript
// hooks/usePermission.ts
export function usePermission(resource: string, action: string) {
  const user = useAuthStore(state => state.user);
  
  if (!user) return false;
  if (user.role === "Admin") return true;
  
  return user.permissions?.includes(`${resource}:${action}`) ?? false;
}
```

---

## 八、API 集成

### 8.1 API 客户端

```typescript
// services/api/client.ts
import axios from "axios";
import { useAuthStore } from "@/stores/authStore";

export const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || "/api",
  timeout: 30000,
});

// 请求拦截器
apiClient.interceptors.request.use((config) => {
  const token = useAuthStore.getState().token;
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

// 响应拦截器
apiClient.interceptors.response.use(
  (response) => response.data,
  (error) => {
    if (error.response?.status === 401) {
      useAuthStore.getState().logout();
      window.location.href = "/auth/login";
    }
    return Promise.reject(error);
  }
);
```

---

## 九、测试要求

### 9.1 单元测试

- 组件渲染逻辑
- 工具函数
- Hooks 逻辑

### 9.2 集成测试

- 用户交互流程
- API 集成
- 路由导航

---

## 十、构建与部署

### 10.1 构建命令

```bash
# 开发
npm run dev

# 构建
npm run build

# 预览
npm run preview

# 代码检查
npm run lint

# 格式化
npm run format
```

### 10.2 环境变量

```env
# .env.development
VITE_API_BASE_URL=http://localhost:5000/api

# .env.production
VITE_API_BASE_URL=/api
```

---

## 十一、注意事项

1. **类型安全**: 所有 API 响应使用 TypeScript 类型
2. **错误处理**: 用户友好的错误提示
3. **加载状态**: 使用 Skeleton 或 Spinner 显示加载状态
4. **无障碍**: 遵循 WCAG 2.1 标准
5. **性能优化**: 使用 React.memo、useMemo、useCallback 优化渲染

---

**最后更新**: 2026-04-28  
**维护者**: 前端实现专家 Agent
