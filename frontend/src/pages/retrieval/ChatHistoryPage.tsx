import { PageHeader } from '@/components/shared/PageHeader'
import { ConversationHistoryPanel } from '@/components/chat/ConversationHistoryPanel'

export function ChatHistoryPage() {
  return (
    <div className="mx-auto w-full max-w-7xl p-4 md:p-8">
      <PageHeader title="历史会话" description="查看和管理过往问答记录" />

      <ConversationHistoryPanel mode="page" />
    </div>
  )
}
