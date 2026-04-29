import { PageHeader } from '@/components/shared/PageHeader'
import { Card, CardContent } from '@/components/ui/Card'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { MessageSquare, Clock } from 'lucide-react'
import { Link } from 'react-router-dom'

export function ChatHistoryPage() {
  const history = [
    { id: 'conv-001', title: 'Q1季度营收分析', turns: 8, time: '10分钟前', status: 'active' },
    { id: 'conv-002', title: '技术架构选型讨论', turns: 12, time: '2小时前', status: 'active' },
    { id: 'conv-003', title: '产品需求梳理', turns: 5, time: '昨天', status: 'inactive' },
    { id: 'conv-004', title: '人力资源政策咨询', turns: 3, time: '昨天', status: 'inactive' },
    { id: 'conv-005', title: '服务器资源申请流程', turns: 6, time: '3天前', status: 'inactive' },
  ]

  return (
    <div>
      <PageHeader title="历史会话" description="查看和管理过往问答记录" />

      <div className="space-y-3">
        {history.map((item) => (
          <Link key={item.id} to={`/retrieval/history/${item.id}`}>
            <Card className="hover:shadow-md transition-shadow cursor-pointer">
              <CardContent className="py-4">
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-3">
                    <div className="w-10 h-10 rounded-lg bg-blue-50 flex items-center justify-center">
                      <MessageSquare className="w-5 h-5 text-blue-600" />
                    </div>
                    <div>
                      <p className="text-sm font-medium text-gray-900">{item.title}</p>
                      <div className="flex items-center gap-3 mt-1">
                        <span className="text-xs text-gray-400 flex items-center gap-1">
                          <Clock className="w-3 h-3" />
                          {item.time}
                        </span>
                        <span className="text-xs text-gray-400">{item.turns} 轮对话</span>
                      </div>
                    </div>
                  </div>
                  <StatusBadge status={item.status} />
                </div>
              </CardContent>
            </Card>
          </Link>
        ))}
      </div>
    </div>
  )
}
