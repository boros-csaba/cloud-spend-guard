import { Alert, Anchor, Button, Card, Group, List, Loader, Stack, Text, ThemeIcon, Title } from '@mantine/core'
import { IconAlertTriangle, IconCheck, IconExternalLink, IconX } from '@tabler/icons-react'
import { useCallback, useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router'
import { type Check, type Connection, useApi } from '../api.ts'
import { ConnectionState } from '../ConnectionState.tsx'

const checkLabels: Record<Check['name'], string> = {
  role: 'Role',
  permissions: 'Permissions',
  costExport: 'Cost Export',
  accountType: 'Account type',
}

const checkIcons = {
  ok: <ThemeIcon color="green" size="sm" radius="xl"><IconCheck size={14} /></ThemeIcon>,
  warning: <ThemeIcon color="yellow" size="sm" radius="xl"><IconAlertTriangle size={14} /></ThemeIcon>,
  failed: <ThemeIcon color="red" size="sm" radius="xl"><IconX size={14} /></ThemeIcon>,
}

export function ConnectionDetail() {
  const { id } = useParams()
  const api = useApi()
  const navigate = useNavigate()
  const [connection, setConnection] = useState<Connection>()
  const [error, setError] = useState<string>()
  const [verifying, setVerifying] = useState(false)
  const [confirmDelete, setConfirmDelete] = useState(false)

  const verify = useCallback(async () => {
    setVerifying(true)

    try {
      setConnection(await api<Connection>(`/connections/${id}/verifications`, { method: 'POST' }))
      setError(undefined)
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setVerifying(false)
    }
  }, [api, id])

  useEffect(() => {
    api<Connection>(`/connections/${id}`).then(setConnection, (e: Error) => setError(e.message))
  }, [api, id])

  const pending = connection?.state === 'pending'

  useEffect(() => {
    if (!pending) {
      return
    }

    const timer = setInterval(verify, 15000)
    return () => clearInterval(timer)
  }, [pending, verify])

  async function remove() {
    try {
      await api(`/connections/${id}`, { method: 'DELETE' })
      navigate('/connections')
    } catch (e) {
      setError((e as Error).message)
      setConfirmDelete(false)
    }
  }

  if (!connection) {
    return error ? <Alert color="red">{error}</Alert> : <Loader />
  }

  return (
    <Stack maw={720}>
      <Group>
        <Title order={2}>AWS account {connection.accountId}</Title>
        <ConnectionState connection={connection} />
      </Group>

      {pending && (
        <Card withBorder>
          <Stack align="flex-start">
            <Title order={4}>Set up access</Title>
            <List type="ordered" spacing="xs">
              <List.Item>Sign in to the AWS console in account {connection.accountId}.</List.Item>
              <List.Item>
                Launch the stack. It opens CloudFormation in us-east-1 with everything filled in. Tick the IAM
                acknowledgement and choose Create stack.
              </List.Item>
              <List.Item>Wait until the stack is complete, about two minutes. This page checks every 15 seconds.</List.Item>
            </List>
            <Text size="sm" c="dimmed">If the stack fails, delete it in CloudFormation before launching it again.</Text>
            <Group>
              <Button component="a" href={connection.launchUrl} target="_blank" rel="noreferrer" rightSection={<IconExternalLink size={16} />}>
                Launch stack
              </Button>
              <Anchor href={connection.templateUrl} target="_blank" rel="noreferrer" size="sm">View template</Anchor>
            </Group>
          </Stack>
        </Card>
      )}

      {connection.state === 'working' && (
        <Text c="dimmed">Cost data usually arrives within 24 to 72 hours of setup.</Text>
      )}

      <Card withBorder>
        <Stack>
          <Group justify="space-between">
            <Title order={4}>Verification</Title>
            {connection.state !== 'working' && (
              <Button variant="default" size="xs" onClick={verify} loading={verifying}>Verify now</Button>
            )}
          </Group>
          {error && <Alert color="red">{error}</Alert>}
          {!connection.verification && <Text c="dimmed">Not verified yet.</Text>}
          {connection.verification && (
            <>
              <List spacing="xs" center>
                {connection.verification.checks.map((check) => (
                  <List.Item key={check.name} icon={checkIcons[check.status]}>
                    <Text span fw={500}>{checkLabels[check.name]}</Text>
                    {check.message && <Text span c="dimmed"> {check.message}</Text>}
                  </List.Item>
                ))}
              </List>
              <Text size="xs" c="dimmed">Last checked {new Date(connection.verification.checkedAt).toLocaleString()}</Text>
            </>
          )}
        </Stack>
      </Card>

      <Group>
        {confirmDelete ? (
          <>
            <Text size="sm">Delete this connection? The stack in your AWS account stays until you delete it.</Text>
            <Button color="red" size="xs" onClick={remove}>Delete</Button>
            <Button variant="default" size="xs" onClick={() => setConfirmDelete(false)}>Cancel</Button>
          </>
        ) : (
          <Button color="red" variant="subtle" size="xs" onClick={() => setConfirmDelete(true)}>Delete connection</Button>
        )}
      </Group>
    </Stack>
  )
}
