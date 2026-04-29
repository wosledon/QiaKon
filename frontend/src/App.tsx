import { AuthProvider } from '@/stores/authStore'
import { RouterProvider } from 'react-router-dom'
import { router } from '@/router'

export default function App() {
  return (
    <AuthProvider>
      <RouterProvider router={router} />
    </AuthProvider>
  )
}
