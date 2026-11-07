import { Alert, Button, Card, Loader, Stack, Text, Title } from '@mantine/core'
import { useEffect, useState } from 'react'
import { Link } from 'react-router'
import { type Connection, useApi } from '../api.ts'
import { EmptyState } from '../EmptyState.tsx'

export function Home() {
  const api = useApi()
  const [connections, setConnections] = useState<Connection[]>()
  const [error, setError] = useState<string>()

  useEffect(() => {
    api<Connection[]>('/connections').then(setConnections, (e: Error) => setError(e.message))
  }, [api])

  const working = connections?.filter((connection) => connection.state === 'working').length ?? 0

  return (
    <Stack>
      <Title order={2}>Home</Title>
      {error && <Alert color="red">{error}</Alert>}
      {!connections && !error && <Loader />}
      {connections?.length === 0 && (
        <EmptyState
          title="Connect your AWS account"
          description="Cloud Spend Guard needs a Connection before it can discover your resources and their cost."
          action="Connect AWS account"
          to="/connections/new"
        />
      )}
      {connections && connections.length > 0 && (
        <Card withBorder padding="xl" maw={520}>
          <Stack align="flex-start">
            <Title order={4}>
              {working} of {connections.length} {connections.length === 1 ? 'connection' : 'connections'} working
            </Title>
            <Text c="dimmed">Your resources and their cost will appear here once discovery runs.</Text>
            <Button component={Link} to="/connections" variant="default">View connections</Button>
          </Stack>
        </Card>
      )}
    </Stack>
  )
}
