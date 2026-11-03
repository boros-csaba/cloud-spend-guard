import { Button, Card, Stack, Text, Title } from '@mantine/core'
import { Link } from 'react-router'

export function EmptyState({ title, description, action, to }: {
  title: string
  description: string
  action: string
  to: string
}) {
  return (
    <Card withBorder padding="xl" maw={520}>
      <Stack align="flex-start">
        <Title order={4}>{title}</Title>
        <Text c="dimmed">{description}</Text>
        <Button component={Link} to={to}>
          {action}
        </Button>
      </Stack>
    </Card>
  )
}
