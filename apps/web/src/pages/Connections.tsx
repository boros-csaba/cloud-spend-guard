import { Button, Group, Stack, Title } from '@mantine/core'
import { IconPlus } from '@tabler/icons-react'
import { Link } from 'react-router'
import { EmptyState } from '../EmptyState.tsx'

export function Connections() {
  return (
    <Stack>
      <Group justify="space-between">
        <Title order={2}>Connections</Title>
        <Button component={Link} to="/connections/new" leftSection={<IconPlus size={16} />}>
          New connection
        </Button>
      </Group>
      <EmptyState
        title="No connections yet"
        description="Connect a single AWS account or a whole organization to start discovering resources."
        action="New connection"
        to="/connections/new"
      />
    </Stack>
  )
}
