import { useAuth } from 'react-oidc-context'

export function useProfile() {
  const profile = useAuth().user?.profile
  const identities = profile?.identities as { providerName: string }[] | undefined
  const email = profile?.email ?? ''

  return {
    name: profile?.name ?? email,
    email,
    picture: profile?.picture,
    provider: identities?.[0]?.providerName,
  }
}
