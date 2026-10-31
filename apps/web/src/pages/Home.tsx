import { useAuth } from 'react-oidc-context'

export function Home({ logoutUrl }: { logoutUrl: string }) {
  const auth = useAuth()

  if (auth.isLoading) {
    return null
  }

  if (auth.isAuthenticated) {
    return (
      <>
        <p>signed in as {auth.user?.profile.email}</p>
        <button
          onClick={async () => {
            await auth.removeUser()
            window.location.href = logoutUrl
          }}
        >
          Sign out
        </button>
      </>
    )
  }

  return (
    <>
      {auth.error && <p>{auth.error.message}</p>}
      <button onClick={() => auth.signinRedirect()}>Get started</button>
    </>
  )
}
