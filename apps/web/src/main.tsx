import '@mantine/core/styles.css'
import { MantineProvider } from '@mantine/core'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { AuthProvider } from 'react-oidc-context'
import { BrowserRouter } from 'react-router'
import { App } from './App.tsx'

const config = await (await fetch('/config.json')).json()
const origin = window.location.origin
const logoutUrl = `${config.loginDomain}/logout?client_id=${config.clientId}&logout_uri=${encodeURIComponent(origin)}`

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <MantineProvider forceColorScheme="dark">
      <AuthProvider
        authority={config.authority}
        client_id={config.clientId}
        redirect_uri={origin}
        scope="openid email profile aws.cognito.signin.user.admin"
        onSigninCallback={() => window.history.replaceState({}, document.title, window.location.pathname)}
      >
        <BrowserRouter>
          <App logoutUrl={logoutUrl} />
        </BrowserRouter>
      </AuthProvider>
    </MantineProvider>
  </StrictMode>,
)
