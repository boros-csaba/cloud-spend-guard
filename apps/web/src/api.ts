import { useCallback } from 'react'
import { useAuth } from 'react-oidc-context'

export type Check = {
  name: 'role' | 'permissions' | 'costExport' | 'accountType'
  status: 'ok' | 'failed' | 'warning'
  message: string | null
}

export type Connection = {
  id: string
  type: 'standalone'
  accountId: string
  createdAt: string
  state: 'pending' | 'working' | 'broken'
  verification: {
    status: 'verified' | 'failed'
    checks: Check[]
    verifiedAt: string
    failingSince: string | null
  } | null
  launchUrl: string
  templateUrl: string
}

export function useApi() {
  const token = useAuth().user?.access_token

  return useCallback(async <T>(path: string, init?: RequestInit): Promise<T> => {
    const response = await fetch(`/api${path}`, {
      ...init,
      headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
    })

    if (!response.ok) {
      const body = await response.json().catch(() => null)
      throw new Error(body?.error ?? `Request failed with status ${response.status}.`)
    }

    return response.status === 204 ? (undefined as T) : response.json()
  }, [token])
}
