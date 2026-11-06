import { Alert, Button, Stack, Text, TextInput, Title } from '@mantine/core'
import { type FormEvent, useState } from 'react'
import { useNavigate } from 'react-router'
import { type Connection, useApi } from '../api.ts'

export function NewConnection() {
  const api = useApi()
  const navigate = useNavigate()
  const [accountId, setAccountId] = useState('')
  const [error, setError] = useState<string>()
  const [saving, setSaving] = useState(false)

  async function submit(event: FormEvent) {
    event.preventDefault()
    setSaving(true)
    setError(undefined)

    try {
      const connection = await api<Connection>('/connections', { method: 'POST', body: JSON.stringify({ accountId }) })
      navigate(`/connections/${connection.id}`)
    } catch (e) {
      setError((e as Error).message)
      setSaving(false)
    }
  }

  return (
    <Stack maw={520}>
      <Title order={2}>New connection</Title>
      <Text c="dimmed">
        Connect one AWS account. Next you launch a CloudFormation stack in that account that gives Cloud Spend Guard
        read-only access and creates a daily cost export in a bucket you own.
      </Text>
      <form onSubmit={submit}>
        <Stack align="flex-start">
          <TextInput
            label="AWS account ID"
            placeholder="123456789012"
            value={accountId}
            onChange={(event) => setAccountId(event.currentTarget.value)}
            required
            w={240}
          />
          {error && <Alert color="red">{error}</Alert>}
          <Button type="submit" loading={saving}>Continue</Button>
        </Stack>
      </form>
    </Stack>
  )
}
