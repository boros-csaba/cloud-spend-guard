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
  type: 'ec2:vpc' | 'ec2:subnet' | 'ec2:volume' | 'ec2:instance' | 's3:bucket' | 'cloudfront:distribution'
    | 'ec2:elastic-ip' | 'ec2:natgateway' | 'ec2:vpn-connection' | 'ec2:vpc-endpoint' | 'ec2:transit-gateway-attachment'
    | 'elasticloadbalancing:loadbalancer' | 'elasticloadbalancing:classic-loadbalancer' | 'rds:db' | 'ec2:snapshot'
    | 'ec2:security-group' | 'ec2:internet-gateway' | 'ec2:route-table' | 'lambda:function' | 'dynamodb:table' | 'apigateway:restapi'
    | 'apigateway:api' | 'states:statemachine' | 'ecs:cluster' | 'ecs:service' | 'ecr:repository' | 'logs:log-group'
    | 'cognito-idp:userpool' | 'cloudformation:stack' | 'route53:hostedzone'
  region: string
  name: string | null
  details: Record<string, string>
  tags: Record<string, string>
  firstSeen: string
  idle: {
    reason: 'unattached' | 'stopped' | 'instanceStopped' | 'unassociated' | 'noRoutes' | 'tunnelsDown' | 'noTargets' | 'noHealthyTargets'
      | 'orphaned'
    since: string
  } | null
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
    scans: { region: string | null, type: Resource['type'], status: 'scanned' | 'skipped' | 'failed', message: string | null }[] | null
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
