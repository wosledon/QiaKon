import { PageHeader } from '@/components/shared/PageHeader'
import { StatCard } from '@/components/shared/StatCard'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { Card, CardHeader, CardContent } from '@/components/ui/Card'
import { Layers, Clock, CheckCircle, AlertTriangle } from 'lucide-react'

export function DocumentIndexPage() {
  return (
    <div>
      <PageHeader title="索引管理" description="查看索引队列、统计与批量操作" />

      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 mb-6">
        <StatCard title="总块数" value="45,231" icon={<Layers className="w-5 h-5" />} color="blue" />
        <StatCard title="平均耗时" value="3.2s" icon={<Clock className="w-5 h-5" />} color="amber" />
        <StatCard title="成功率" value="98.7%" icon={<CheckCircle className="w-5 h-5" />} color="green" />
        <StatCard title="失败任务" value="12" icon={<AlertTriangle className="w-5 h-5" />} color="rose" />
      </div>

      <Card>
        <CardHeader>
          <h3 className="text-base font-semibold text-gray-900">索引队列</h3>
        </CardHeader>
        <CardContent>
          <div className="space-y-3">
            {[
              { doc: '2026技术规划.pdf', status: 'indexing', progress: 65 },
              { doc: '产品PRD.md', status: 'pending', progress: 0 },
              { doc: '运营数据.xlsx', status: 'completed', progress: 100 },
              { doc: '客户访谈.docx', status: 'failed', progress: 30 },
            ].map((item, i) => (
              <div key={i} className="flex items-center gap-4 py-3 border-b border-gray-50 last:border-0">
                <div className="flex-1 min-w-0">
                  <p className="text-sm font-medium text-gray-900">{item.doc}</p>
                  <div className="mt-2 w-full bg-gray-100 rounded-full h-1.5">
                    <div
                      className={`h-1.5 rounded-full transition-all ${
                        item.status === 'failed' ? 'bg-rose-500' : 'bg-blue-500'
                      }`}
                      style={{ width: `${item.progress}%` }}
                    />
                  </div>
                </div>
                <StatusBadge status={item.status} />
              </div>
            ))}
          </div>
        </CardContent>
      </Card>
    </div>
  )
}
