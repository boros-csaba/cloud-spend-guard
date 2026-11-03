import { Button, Card, PasswordInput, Stack, Text, Title } from '@mantine/core'
import { useState, type FormEvent } from 'react'
import { useAuth } from 'react-oidc-context'

export function ChangePasswordForm() {
  const auth = useAuth()
  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [error, setError] = useState<string>()
  const [changed, setChanged] = useState(false)
  const [submitting, setSubmitting] = useState(false)

  async function submit(event: FormEvent) {
    event.preventDefault()
    setError(undefined)
    setChanged(false)

    if (newPassword !== confirmPassword) {
      setError('The new passwords do not match.')
      return
    }

    setSubmitting(true)

    try {
      const response = await fetch(new URL(auth.settings.authority).origin, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/x-amz-json-1.1',
          'X-Amz-Target': 'AWSCognitoIdentityProviderService.ChangePassword',
        },
        body: JSON.stringify({
          AccessToken: auth.user?.access_token,
          PreviousPassword: currentPassword,
          ProposedPassword: newPassword,
        }),
      })

      if (!response.ok) {
        setError((await response.json()).message)
        return
      }

      setCurrentPassword('')
      setNewPassword('')
      setConfirmPassword('')
      setChanged(true)
    } catch {
      setError('Could not change the password. Try again.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <Card withBorder padding="xl" maw={520}>
      <form onSubmit={submit}>
        <Stack>
          <Title order={4}>Change password</Title>
          <PasswordInput
            label="Current password"
            value={currentPassword}
            onChange={(event) => setCurrentPassword(event.currentTarget.value)}
            required
          />
          <PasswordInput
            label="New password"
            value={newPassword}
            onChange={(event) => setNewPassword(event.currentTarget.value)}
            required
          />
          <PasswordInput
            label="Confirm new password"
            value={confirmPassword}
            onChange={(event) => setConfirmPassword(event.currentTarget.value)}
            required
          />
          {error && <Text c="red" size="sm">{error}</Text>}
          {changed && <Text c="green" size="sm">Password changed.</Text>}
          <Button type="submit" loading={submitting} style={{ alignSelf: 'flex-start' }}>
            Change password
          </Button>
        </Stack>
      </form>
    </Card>
  )
}
