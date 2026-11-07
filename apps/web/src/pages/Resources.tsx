import { Alert, Anchor, Badge, Button, Card, Group, Loader, Stack, Table, Text, Title } from '@mantine/core'
import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router'
import { type CloudAccount, type Resource, useApi } from '../api.ts'
import { EmptyState } from '../EmptyState.tsx'

const typeLabels: Record<Resource['type'], string> = {
  'ec2:vpc': 'VPC',
  'ec2:subnet': 'Subnet',
  'ec2:volume': 'EBS volume',
  'ec2:instance': 'EC2 instance',
  's3:bucket': 'S3 bucket',
  'cloudfront:distribution': 'CloudFront distribution',
  'ec2:elastic-ip': 'Elastic IP',
  'ec2:natgateway': 'NAT gateway',
  'ec2:vpn-connection': 'VPN connection',
  'ec2:vpc-endpoint': 'VPC endpoint',
  'ec2:transit-gateway-attachment': 'Transit Gateway attachment',
  'elasticloadbalancing:loadbalancer': 'Load balancer',
  'elasticloadbalancing:classic-loadbalancer': 'Classic load balancer',
  'rds:db': 'RDS database',
  'ec2:snapshot': 'EBS snapshot',
  'ec2:security-group': 'Security group',
  'ec2:internet-gateway': 'Internet gateway',
  'ec2:route-table': 'Route table',
  'lambda:function': 'Lambda function',
  'dynamodb:table': 'DynamoDB table',
  'apigateway:restapi': 'API Gateway REST API',
  'apigateway:api': 'API Gateway HTTP or WebSocket API',
  'states:statemachine': 'Step Functions state machine',
  'ecs:cluster': 'ECS cluster',
  'ecs:service': 'ECS service',
  'ecr:repository': 'ECR repository',
  'logs:log-group': 'CloudWatch log group',
  'cognito-idp:userpool': 'Cognito user pool',
  'cloudformation:stack': 'CloudFormation stack',
  'route53:hostedzone': 'Route 53 hosted zone',
}

const idleLabels: Record<NonNullable<Resource['idle']>['reason'], string> = {
  unattached: 'Unattached',
  stopped: 'Stopped',
  instanceStopped: 'Instance stopped',
  unassociated: 'Not associated',
  noRoutes: 'No routes',
  tunnelsDown: 'Tunnels down',
  noTargets: 'No targets',
  noHealthyTargets: 'No healthy targets',
  orphaned: 'Orphaned',
}

function details({ type, details }: Resource) {
  switch (type) {
    case 'ec2:vpc':
      return `${details.cidr}${details.isDefault === 'true' ? ', default' : ''}`
    case 'ec2:subnet':
      return `${details.cidr}, ${details.availabilityZone}${details.public === 'true' ? ', public' : ''}`
    case 'ec2:volume':
      return `${details.sizeGiB} GiB ${details.volumeType}${details.attachedTo ? `, attached to ${details.attachedTo}` : ''}`
    case 'ec2:instance':
      return `${details.instanceType}, ${details.state}${details.lifecycle === 'spot' ? ', Spot' : ''}`
    case 's3:bucket':
      return details.createdAt ? `Created ${date(details.createdAt)}` : ''
    case 'cloudfront:distribution':
      return `${details.aliases || details.domainName}, ${details.enabled === 'true' ? 'enabled' : 'disabled'}`
    case 'ec2:elastic-ip':
      return details.instanceId ? `Associated with ${details.instanceId}` : details.associationId ? 'Associated' : 'Not associated'
    case 'ec2:natgateway':
      return `${details.connectivityType}, ${details.state}${details.routed === 'false' ? ', no routes' : ''}`
    case 'ec2:vpn-connection':
      return `${details.tunnelsUp} of ${details.tunnels} tunnels up, ${details.state}`
    case 'ec2:vpc-endpoint':
      return `${details.endpointType} ${details.serviceName}, ${details.state}`
    case 'ec2:transit-gateway-attachment':
      return `${details.resourceType} ${details.resourceId}, ${details.state}`
    case 'elasticloadbalancing:loadbalancer':
    case 'elasticloadbalancing:classic-loadbalancer':
      return `${details.type || 'classic'}, ${details.healthyTargets} of ${details.registeredTargets} targets healthy`
    case 'rds:db':
      return `${details.engine} ${details.instanceClass}, ${details.storageGiB} GiB, ${details.status}`
    case 'ec2:snapshot':
      return `${details.sizeGiB} GiB from ${details.volumeId}, ${date(details.startedAt)}${details.managedBy ? `, ${details.managedBy}` : ''}`
    case 'ec2:security-group':
      return `${details.vpcId}, ${details.inboundRules} inbound and ${details.outboundRules} outbound rules`
    case 'ec2:internet-gateway':
      return details.vpcId ? `Attached to ${details.vpcId}` : 'Not attached'
    case 'ec2:route-table':
      return `${details.vpcId}${details.main === 'true' ? ', main' : ''}, ${details.subnets} subnets, ${details.routes} routes`
    case 'lambda:function':
      return `${details.runtime}, ${details.memoryMB} MB`
    case 'dynamodb:table':
      return `${details.billingMode === 'PAY_PER_REQUEST' ? 'On-demand' : `Provisioned ${details.readCapacity} read, ${details.writeCapacity} write`}, ${size(details.sizeBytes)}`
    case 'apigateway:restapi':
      return `REST, ${details.endpointType}`
    case 'apigateway:api':
      return details.protocol
    case 'states:statemachine':
      return details.type
    case 'ecs:cluster':
      return `${details.activeServices} services, ${details.runningTasks} running tasks`
    case 'ecs:service':
      return `${details.cluster}, ${details.runningCount} of ${details.desiredCount} tasks running`
    case 'logs:log-group':
      return `${size(details.storedBytes)}, ${details.retentionDays ? `kept ${details.retentionDays} days` : 'kept forever'}`
    case 'cloudformation:stack':
      return details.status
    case 'route53:hostedzone':
      return `${details.private === 'true' ? 'Private' : 'Public'}, ${details.records} records`
    case 'ecr:repository':
    case 'cognito-idp:userpool':
      return details.createdAt ? `Created ${date(details.createdAt)}` : ''
  }
}

function resourceId(resource: Resource) {
  return resource.arn.split(/[/:]/).pop()
}

function date(value: string) {
  return new Date(value).toLocaleDateString()
}

function size(bytes: string) {
  const value = Number(bytes)
  return value >= 1e9 ? `${(value / 1e9).toFixed(1)} GB` : value >= 1e6 ? `${(value / 1e6).toFixed(1)} MB` : `${Math.round(value / 1e3)} KB`
}

function order(resource: Resource) {
  return resource.deletedAt ? 2 : resource.idle ? 0 : 1
}

function ResourceStatus({ resource }: { resource: Resource }) {
  if (resource.deletedAt) {
    return <Badge color="gray">Deleted {date(resource.deletedAt)}</Badge>
  }

  if (resource.idle) {
    return <Badge color="orange">Idle: {idleLabels[resource.idle.reason]} since {date(resource.idle.since)}</Badge>
  }

  return null
}

function CloudAccountResources({ account, onRun }: { account: CloudAccount, onRun: () => Promise<void> }) {
  const [starting, setStarting] = useState(false)
  const running = account.run?.status === 'running'
  const notScanned = account.snapshot?.scans?.filter((scan) => scan.status !== 'scanned') ?? []
  const resources = [...account.snapshot?.resources ?? []].sort((a, b) => order(a) - order(b))

  async function run() {
    setStarting(true)
    await onRun()
    setStarting(false)
  }

  return (
    <Card withBorder>
      <Stack>
        <Group justify="space-between">
          <Title order={4}>AWS account {account.accountId}</Title>
          <Button size="xs" variant="default" onClick={run} loading={starting || running} disabled={account.connectionState === 'pending'}>
            Run Snapshot
          </Button>
        </Group>
        {account.connectionState === 'pending' && (
          <Text c="dimmed">
            Finish setting up the <Anchor component={Link} to={`/connections/${account.connectionId}`}>Connection</Anchor> first.
          </Text>
        )}
        {running && <Text c="dimmed">Snapshot running...</Text>}
        {account.run?.status === 'failed' && (
          <Alert color="red">
            {account.run.message} <Anchor component={Link} to={`/connections/${account.connectionId}`}>Open the Connection</Anchor>
          </Alert>
        )}
        {account.snapshot && (
          <Text size="sm" c="dimmed">
            Snapshot of {new Date(account.snapshot.takenAt).toLocaleString()}: {account.run?.resourceCount ?? resources.length} resources,{' '}
            {account.run?.idleCount ?? 0} Idle.
          </Text>
        )}
        {notScanned.length > 0 && (
          <Text size="sm" c="dimmed">
            Not scanned: {notScanned.map((scan) => `${typeLabels[scan.type]}${scan.region ? ` in ${scan.region}` : ''} (${scan.message})`).join(', ')}
          </Text>
        )}
        {!account.snapshot && !running && account.connectionState !== 'pending' && (
          <Text c="dimmed">No Snapshot yet. Run one to see this account's resources.</Text>
        )}
        {resources.length > 0 && (
          <Table>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Resource</Table.Th>
                <Table.Th>Type</Table.Th>
                <Table.Th>Region</Table.Th>
                <Table.Th>Details</Table.Th>
                <Table.Th>Status</Table.Th>
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {resources.map((resource) => (
                <Table.Tr key={resource.arn}>
                  <Table.Td>
                    <Text size="sm">{resource.name ?? resourceId(resource)}</Text>
                    {resource.name && resource.name !== resourceId(resource) && <Text size="xs" c="dimmed">{resourceId(resource)}</Text>}
                  </Table.Td>
                  <Table.Td>{typeLabels[resource.type]}</Table.Td>
                  <Table.Td>{resource.region}</Table.Td>
                  <Table.Td>{details(resource)}</Table.Td>
                  <Table.Td><ResourceStatus resource={resource} /></Table.Td>
                </Table.Tr>
              ))}
            </Table.Tbody>
          </Table>
        )}
      </Stack>
    </Card>
  )
}

export function Resources() {
  const api = useApi()
  const [accounts, setAccounts] = useState<CloudAccount[]>()
  const [error, setError] = useState<string>()

  const load = useCallback(async () => {
    try {
      setAccounts(await api<CloudAccount[]>('/snapshots'))
      setError(undefined)
    } catch (e) {
      setError((e as Error).message)
    }
  }, [api])

  useEffect(() => {
    api<CloudAccount[]>('/snapshots').then(setAccounts, (e: Error) => setError(e.message))
  }, [api])

  const running = accounts?.some((account) => account.run?.status === 'running') ?? false

  useEffect(() => {
    if (!running) {
      return
    }

    const timer = setInterval(load, 5000)
    return () => clearInterval(timer)
  }, [running, load])

  async function run(account: CloudAccount) {
    try {
      await api(`/connections/${account.connectionId}/snapshots`, { method: 'POST' })
      await load()
    } catch (e) {
      setError((e as Error).message)
    }
  }

  return (
    <Stack>
      <Title order={2}>Resources</Title>
      {error && <Alert color="red">{error}</Alert>}
      {!accounts && !error && <Loader />}
      {accounts?.length === 0 && (
        <EmptyState
          title="No resources yet"
          description="Resources appear here after the first Snapshot of a verified Connection."
          action="Go to Connections"
          to="/connections"
        />
      )}
      {accounts?.map((account) => (
        <CloudAccountResources key={account.accountId} account={account} onRun={() => run(account)} />
      ))}
    </Stack>
  )
}
