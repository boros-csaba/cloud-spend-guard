import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { AuthProvider } from 'react-oidc-context'
import { Home } from './pages/Home.tsx'

const config = await (await fetch('/config.json')).json()
const origin = window.location.origin
const logoutUrl = `${config.loginDomain}/logout?client_id=${config.clientId}&logout_uri=${encodeURIComponent(origin)}`

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <AuthProvider
      authority={config.authority}
      client_id={config.clientId}
      redirect_uri={origin}
      scope="openid email profile"
      onSigninCallback={() => window.history.replaceState({}, document.title, window.location.pathname)}
    >
      <Home logoutUrl={logoutUrl} />
    </AuthProvider>
  </StrictMode>,
)
