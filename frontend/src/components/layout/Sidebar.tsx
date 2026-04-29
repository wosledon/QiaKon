import { useState } from 'react'
import { Link, useLocation } from 'react-router-dom'
import {
  LayoutDashboard,
  FileText,
  Network,
  MessageSquare,
  Workflow,
  Settings,
  Shield,
  ChevronRight,
  type LucideIcon,
} from 'lucide-react'

interface NavItem {
  label: string
  icon: LucideIcon
  href?: string
  children?: { label: string; href: string }[]
}

const navGroups: { label: string; items: NavItem[] }[] = [
  {
    label: '核心功能',
    items: [
      { label: '首页 / 对话', icon: MessageSquare, href: '/' },
      { label: '工作台', icon: LayoutDashboard, href: '/dashboard' },
    ],
  },
  {
    label: '知识管理',
    items: [
      {
        label: '文档管理',
        icon: FileText,
        href: '/documents',
        children: [
          { label: '文档列表', href: '/documents' },
          { label: '上传文档', href: '/documents/upload' },
          { label: '索引管理', href: '/documents/index' },
        ],
      },
      {
        label: '知识图谱',
        icon: Network,
        href: '/graphs',
        children: [
          { label: '图谱概览', href: '/graphs' },
          { label: '实体管理', href: '/graphs/entities' },
          { label: '关系管理', href: '/graphs/relations' },
          { label: '图谱查询', href: '/graphs/query' },
        ],
      },
      {
        label: '检索问答',
        icon: MessageSquare,
        href: '/retrieval/chat',
        children: [
          { label: '智能问答', href: '/retrieval/chat' },
          { label: '历史记录', href: '/retrieval/history' },
        ],
      },
    ],
  },
  {
    label: '工作流',
    items: [
      {
        label: '工作流编排',
        icon: Workflow,
        href: '/workflows',
        children: [
          { label: '工作流列表', href: '/workflows' },
          { label: '运行记录', href: '/workflows/runs' },
        ],
      },
    ],
  },
  {
    label: '系统管理',
    items: [
      {
        label: '权限管理',
        icon: Shield,
        href: '/admin/departments',
        children: [
          { label: '部门管理', href: '/admin/departments' },
          { label: '角色管理', href: '/admin/roles' },
          { label: '用户管理', href: '/admin/users' },
        ],
      },
      {
        label: '系统配置',
        icon: Settings,
        href: '/admin/config',
        children: [
          { label: 'LLM 模型', href: '/admin/llm-models' },
          { label: '系统配置', href: '/admin/config' },
          { label: '连接器', href: '/admin/connectors' },
          { label: '审计日志', href: '/admin/audit' },
          { label: '健康检查', href: '/admin/health' },
        ],
      },
    ],
  },
]

function isActive(locationPath: string, href: string, isChild = false): boolean {
  if (href === '/') return locationPath === '/'
  if (isChild) return locationPath === href
  return locationPath === href || locationPath.startsWith(`${href}/`)
}

function NavGroup({ group, locationPath }: { group: (typeof navGroups)[number]; locationPath: string }) {
  return (
    <div className="px-3 py-2">
      <p className="px-3 text-[11px] font-semibold text-gray-400 uppercase tracking-wider mb-1">
        {group.label}
      </p>
      <ul className="space-y-0.5">
        {group.items.map((item) => (
          <NavItemComponent key={item.label} item={item} locationPath={locationPath} />
        ))}
      </ul>
    </div>
  )
}

function NavItemComponent({ item, locationPath }: { item: NavItem; locationPath: string }) {
  const hasChildren = item.children && item.children.length > 0
  const isItemActive = item.href ? isActive(locationPath, item.href) : false
  const isChildActive = hasChildren
    ? item.children!.some((c) => isActive(locationPath, c.href))
    : false
  const expanded = isChildActive

  const [open, setOpen] = useState(expanded)

  const showExpanded = open || isChildActive

  if (!hasChildren && item.href) {
    return (
      <li>
        <Link
          to={item.href}
          className={`flex items-center gap-3 px-3 py-2 rounded-lg text-sm font-medium transition-colors ${
            isItemActive
              ? 'bg-blue-50 text-blue-700'
              : 'text-gray-600 hover:bg-gray-50 hover:text-gray-900'
          }`}
        >
          <item.icon className={`w-4.5 h-4.5 flex-shrink-0 ${isItemActive ? 'text-blue-600' : 'text-gray-400'}`} />
          <span className="truncate">{item.label}</span>
        </Link>
      </li>
    )
  }

  return (
    <li>
      <button
        onClick={() => setOpen(!open)}
        className={`w-full flex items-center gap-3 px-3 py-2 rounded-lg text-sm font-medium transition-colors ${
          isItemActive ? 'text-blue-700' : 'text-gray-600 hover:bg-gray-50 hover:text-gray-900'
        }`}
      >
        <item.icon className={`w-4.5 h-4.5 flex-shrink-0 ${isItemActive ? 'text-blue-600' : 'text-gray-400'}`} />
        <span className="flex-1 text-left truncate">{item.label}</span>
        <ChevronRight
          className={`w-4 h-4 text-gray-400 transition-transform flex-shrink-0 ${showExpanded ? 'rotate-90' : ''}`}
        />
      </button>
      {showExpanded && item.children && (
        <ul className="mt-0.5 ml-2 pl-4 border-l border-gray-200 space-y-0.5">
          {item.children.map((child) => {
            const childActive = isActive(locationPath, child.href, true)
            return (
              <li key={child.href}>
                <Link
                  to={child.href}
                  className={`flex items-center gap-2 px-3 py-1.5 rounded-lg text-sm transition-colors ${
                    childActive
                      ? 'bg-blue-50 text-blue-700 font-medium'
                      : 'text-gray-500 hover:bg-gray-50 hover:text-gray-900'
                  }`}
                >
                  <span className={`w-1.5 h-1.5 rounded-full flex-shrink-0 ${childActive ? 'bg-blue-500' : 'bg-gray-300'}`} />
                  <span className="truncate">{child.label}</span>
                </Link>
              </li>
            )
          })}
        </ul>
      )}
    </li>
  )
}

interface SidebarProps {
  mobileOpen: boolean
  onClose: () => void
}

export function Sidebar({ mobileOpen, onClose }: SidebarProps) {
  const location = useLocation()

  return (
    <>
      {/* Mobile overlay */}
      {mobileOpen && (
        <div
          className="fixed inset-0 bg-black/30 backdrop-blur-sm z-40 lg:hidden"
          onClick={onClose}
        />
      )}

      {/* Sidebar */}
      <aside
        className={`fixed lg:sticky top-0 left-0 z-50 h-screen w-64 bg-white border-r border-gray-200 flex flex-col transition-transform duration-300 ease-in-out lg:translate-x-0 ${
          mobileOpen ? 'translate-x-0' : '-translate-x-full'
        }`}
      >
        <div className="flex items-center gap-2.5 h-16 px-5 border-b border-gray-200 lg:hidden">
          <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-blue-600 to-indigo-600 flex items-center justify-center">
            <span className="text-white font-bold text-sm">Q</span>
          </div>
          <span className="text-lg font-bold text-gray-900 tracking-tight">QiaKon</span>
        </div>

        <nav className="flex-1 overflow-y-auto py-3 space-y-1">
          {navGroups.map((group) => (
            <NavGroup key={group.label} group={group} locationPath={location.pathname} />
          ))}
        </nav>

        <div className="px-4 py-3 border-t border-gray-200">
          <div className="bg-gray-50 rounded-lg px-3 py-2.5">
            <p className="text-[11px] font-medium text-gray-500">QiaKon KAG 平台</p>
            <p className="text-[10px] text-gray-400 mt-0.5">v1.0 · 企业版</p>
          </div>
        </div>
      </aside>
    </>
  )
}
