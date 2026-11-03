import { Stack, Title } from '@mantine/core'
import { EmptyState } from '../EmptyState.tsx'

export function Home() {
  return (
    <Stack>
      <Title order={2}>Home</Title>
      <EmptyState
        title="Connect your AWS account"
        description="Cloud Spend Guard needs a Connection before it can discover your resources and their cost."
        action="Connect AWS account"
        to="/connections/new"
      />
    </Stack>
  )
}
