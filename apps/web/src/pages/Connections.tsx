import { Alert, Anchor, Button, Group, Loader, Stack, Table, Title } from '@mantine/core'
import { IconPlus } from '@tabler/icons-react'
import { useEffect, useState } from 'react'
import { Link } from 'react-router'
import { type Connection, useApi } from '../api.ts'
import { ConnectionState } from '../ConnectionState.tsx'
import { EmptyState } from '../EmptyState.tsx'

export function Connections() {
  const api = useApi()
  const [connections, setConnections] = useState<Connection[]>()
  const [error, setError] = useState<string>()

  useEffect(() => {
    api<Connection[]>('/connections').then(setConnections, (e: Error) => setError(e.message))
  }, [api])

  return (
    <Stack>
      <Group justify="space-between">
        <Title order={2}>Connections</Title>
        <Button component={Link} to="/connections/new" leftSection={<IconPlus size={16} />}>
          New connection
        </Button>
      </Group>
      {error && <Alert color="red">{error}</Alert>}
      {!connections && !error && <Loader />}
      {connections?.length === 0 && (
        <EmptyState
          title="No connections yet"
          description="Connect a single AWS account or a whole organization to start discovering resources."
          action="New connection"
          to="/connections/new"
        />
      )}
      {connections && connections.length > 0 && (
        <Table maw={720}>
          <Table.Thead>
            <Table.Tr>
              <Table.Th>AWS account</Table.Th>
              <Table.Th>State</Table.Th>
              <Table.Th>Created</Table.Th>
            </Table.Tr>
          </Table.Thead>
          <Table.Tbody>
            {connections.map((connection) => (
              <Table.Tr key={connection.id}>
                <Table.Td>
                  <Anchor component={Link} to={`/connections/${connection.id}`}>{connection.accountId}</Anchor>
                </Table.Td>
                <Table.Td><ConnectionState connection={connection} /></Table.Td>
                <Table.Td>{new Date(connection.createdAt).toLocaleDateString()}</Table.Td>
              </Table.Tr>
            ))}
          </Table.Tbody>
        </Table>
      )}
    </Stack>
  )
}
