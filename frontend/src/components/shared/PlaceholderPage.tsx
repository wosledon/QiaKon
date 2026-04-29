import { PageHeader } from './PageHeader'
import { Construction } from 'lucide-react'

interface PlaceholderPageProps {
  title: string
  description?: string
}

export function PlaceholderPage({ title, description }: PlaceholderPageProps) {
  return (
    <div>
      <PageHeader title={title} description={description} />
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm">
        <div className="flex flex-col items-center justify-center py-24 px-4">
          <div className="w-16 h-16 rounded-2xl bg-gray-50 flex items-center justify-center mb-4">
            <Construction className="w-8 h-8 text-gray-400" />
          </div>
          <h3 className="text-lg font-semibold text-gray-900">页面建设中</h3>
          <p className="mt-2 text-sm text-gray-500 max-w-sm text-center">
            该功能模块正在开发中，敬请期待后续版本更新。
          </p>
        </div>
      </div>
    </div>
  )
}
