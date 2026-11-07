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
    checkedAt: string
    failingSince: string | null
  } | null
  launchUrl: string
  templateUrl: string
}

export type Resource = {
  arn: string
  type: 'ec2:volume'
  region: string
  name: string | null
  details: Record<string, string>
  tags: Record<string, string>
  firstSeen: string
  idle: { reason: 'unattached', since: string } | null
  deletedAt: string | null
}

export type Run = {
  status: 'running' | 'done' | 'failed'
  startedAt: string
  finishedAt: string | null
  message: string | null
  resourceCount: number | null
  idleCount: number | null
}

export type CloudAccount = {
  connectionId: string
  accountId: string
  connectionState: Connection['state']
  run: Run | null
  snapshot: {
    takenAt: string
    regions: { region: string, status: 'scanned' | 'skipped' | 'failed', message: string | null }[]
    resources: Resource[]
  } | null
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
