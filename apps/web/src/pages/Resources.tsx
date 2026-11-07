import { Alert, Anchor, Badge, Button, Card, Group, Loader, Stack, Table, Text, Title } from '@mantine/core'
import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router'
import { type CloudAccount, type Resource, useApi } from '../api.ts'
import { EmptyState } from '../EmptyState.tsx'

const typeLabels: Record<Resource['type'], string> = {
  'ec2:volume': 'EBS volume',
}

const idleLabels: Record<NonNullable<Resource['idle']>['reason'], string> = {
  unattached: 'Unattached',
}

function date(value: string) {
  return new Date(value).toLocaleDateString()
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
  const notScanned = account.snapshot?.regions.filter((region) => region.status !== 'scanned') ?? []
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
            Not scanned: {notScanned.map((region) => `${region.region} (${region.message})`).join(', ')}
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
                    <Text size="sm">{resource.name ?? resource.arn.split('/').pop()}</Text>
                    {resource.name && <Text size="xs" c="dimmed">{resource.arn.split('/').pop()}</Text>}
                  </Table.Td>
                  <Table.Td>{typeLabels[resource.type]}</Table.Td>
                  <Table.Td>{resource.region}</Table.Td>
                  <Table.Td>{resource.details.sizeGiB} GiB {resource.details.volumeType}</Table.Td>
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
