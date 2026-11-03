import { useAuth } from 'react-oidc-context'
import { Navigate, Route, Routes } from 'react-router'
import { Shell } from './Shell.tsx'
import { Account } from './pages/Account.tsx'
import { ConnectionDetail } from './pages/ConnectionDetail.tsx'
import { Connections } from './pages/Connections.tsx'
import { Home } from './pages/Home.tsx'
import { Landing } from './pages/Landing.tsx'
import { NewConnection } from './pages/NewConnection.tsx'
import { Resources } from './pages/Resources.tsx'

export function App({ logoutUrl }: { logoutUrl: string }) {
  const auth = useAuth()

  if (auth.isLoading) {
    return null
  }

  if (!auth.isAuthenticated) {
    return <Landing />
  }

  return (
    <Routes>
      <Route element={<Shell />}>
        <Route index element={<Home />} />
        <Route path="connections" element={<Connections />} />
        <Route path="connections/new" element={<NewConnection />} />
        <Route path="connections/:id" element={<ConnectionDetail />} />
        <Route path="resources" element={<Resources />} />
        <Route path="account" element={<Account logoutUrl={logoutUrl} />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Route>
    </Routes>
  )
}
