import { Avatar, Button, Card, Group, Stack, Text, Title } from '@mantine/core'
import { useAuth } from 'react-oidc-context'
import { ChangePasswordForm } from '../ChangePasswordForm.tsx'
import { useProfile } from '../useProfile.ts'

export function Account({ logoutUrl }: { logoutUrl: string }) {
  const auth = useAuth()
  const { name, email, picture, provider } = useProfile()

  return (
    <Stack>
      <Title order={2}>Account</Title>
      <Card withBorder padding="xl" maw={520}>
        <Stack align="flex-start">
          <Group>
            <Avatar src={picture} name={name} color="initials" size="lg" />
            <div>
              <Text fw={600}>{name}</Text>
              {name !== email && <Text c="dimmed" size="sm">{email}</Text>}
            </div>
          </Group>
          <Text size="sm">Signed in with {provider ?? 'email and password'}</Text>
          <Button
            variant="default"
            onClick={async () => {
              await auth.removeUser()
              window.location.href = logoutUrl
            }}
          >
            Log out
          </Button>
        </Stack>
      </Card>
      {!provider && <ChangePasswordForm />}
    </Stack>
  )
}
