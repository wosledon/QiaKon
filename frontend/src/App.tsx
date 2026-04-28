import { useState, useEffect } from 'react'
import { AuthProvider, useAuth } from '@/stores/authStore'
import { Layout } from '@/components/Layout'
import { LoginPage } from '@/pages/LoginPage'
import { ChatPage } from '@/pages/ChatPage'
import { DocumentsPage } from '@/pages/DocumentsPage'
import { GraphPage } from '@/pages/GraphPage'

type TabId = 'chat' | 'documents' | 'graph'

function AppContent() {
  const { isAuthenticated } = useAuth()
  const [activeTab, setActiveTab] = useState<TabId>('chat')

  useEffect(() => {
    const hash = window.location.hash.replace('#', '') as TabId
    if (['chat', 'documents', 'graph'].includes(hash)) {
      setActiveTab(hash)
    }
  }, [])

  const handleTabChange = (tab: string) => {
    setActiveTab(tab as TabId)
    window.location.hash = tab
  }

  if (!isAuthenticated) {
    return <LoginPage />
  }

  return (
    <Layout activeTab={activeTab} onTabChange={handleTabChange}>
      {activeTab === 'chat' && <ChatPage />}
      {activeTab === 'documents' && <DocumentsPage />}
      {activeTab === 'graph' && <GraphPage />}
    </Layout>
  )
}

export default function App() {
  return (
    <AuthProvider>
      <AppContent />
    </AuthProvider>
  )
}
