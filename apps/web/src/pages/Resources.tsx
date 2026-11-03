import { Stack, Title } from '@mantine/core'
import { EmptyState } from '../EmptyState.tsx'

export function Resources() {
  return (
    <Stack>
      <Title order={2}>Resources</Title>
      <EmptyState
        title="No resources yet"
        description="Resources appear here after the first Snapshot of a verified Connection."
        action="Go to Connections"
        to="/connections"
      />
    </Stack>
  )
}
