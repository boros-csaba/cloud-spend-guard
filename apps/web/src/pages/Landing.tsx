import { Button, Center, Image, Stack, Text, Title } from '@mantine/core'
import { useAuth } from 'react-oidc-context'

export function Landing() {
  const auth = useAuth()

  return (
    <Center h="100vh">
      <Stack align="center" maw={480} ta="center">
        <Image src="/favicon.svg" w={56} h={56} />
        <Title order={1}>Cloud Spend Guard</Title>
        <Text c="dimmed">
          Connect your AWS accounts and see every resource on a map, with its cost and utilization.
        </Text>
        {auth.error && <Text c="red">{auth.error.message}</Text>}
        <Button size="md" onClick={() => auth.signinRedirect()}>
          Sign in
        </Button>
      </Stack>
    </Center>
  )
}
