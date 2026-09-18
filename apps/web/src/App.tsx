import { useEffect, useState } from 'react'

type Verification = {
  status: 'verified' | 'failed'
  assumeRole: 'ok' | 'failed'
  error: string | null
  permissions: { action: string; allowed: boolean }[]
  verifiedAt: string
}

type Connection = {
  id: string
  externalId: string
  roleArn: string | null
  verification: Verification | null
  roleName: string
  trustPolicy: string
  permissionsPolicy: string
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(path, init)
  const body = await response.json()

  if (!response.ok) {
    throw new Error(body.error ?? `Request failed with status ${response.status}`)
  }

  return body
}

const connectionId = new URLSearchParams(window.location.search).get('connection')

function App() {
  const [connection, setConnection] = useState<Connection | null>(null)
  const [roleArn, setRoleArn] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    if (!connectionId) {
      return
    }

    request<Connection>(`/connections/${connectionId}`)
      .then((loaded) => {
        setConnection(loaded)
        setRoleArn(loaded.roleArn ?? '')
      })
      .catch((error: Error) => setError(error.message))
  }, [])

  async function createConnection() {
    setBusy(true)

    try {
      const created = await request<Connection>('/connections', { method: 'POST' })
      window.location.search = `?connection=${created.id}`
    } catch (error) {
      setError((error as Error).message)
      setBusy(false)
    }
  }

  async function verify() {
    setBusy(true)
    setError(null)

    try {
      setConnection(
        await request<Connection>(`/connections/${connectionId}/verifications`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ roleArn }),
        }),
      )
    } catch (error) {
      setError((error as Error).message)
    } finally {
      setBusy(false)
    }
  }

  if (!connectionId) {
    return (
      <>
        <button onClick={createConnection} disabled={busy}>
          Create Connection
        </button>
        {error && <p>Error: {error}</p>}
      </>
    )
  }

  if (!connection) {
    return <p>{error ? `Error: ${error}` : 'Loading…'}</p>
  }

  const verification = connection.verification
  const verified = verification?.status === 'verified'

  return (
    <>
      <h2>1. Create the Auditor Role</h2>
      <p>
        In the AWS account you want to audit, create an IAM role named <code>{connection.roleName}</code> with
        this trust policy (External ID <code>{connection.externalId}</code>):
      </p>
      <pre>{connection.trustPolicy}</pre>
      <p>and attach this permissions policy:</p>
      <pre>{connection.permissionsPolicy}</pre>

      <h2>2. Verify it</h2>
      <input
        value={roleArn}
        onChange={(event) => setRoleArn(event.target.value)}
        placeholder={`arn:aws:iam::123456789012:role/${connection.roleName}`}
        readOnly={verified}
        size={60}
      />
      <button onClick={verify} disabled={busy || !roleArn}>
        Verify
      </button>
      {error && <p>Error: {error}</p>}

      {verification && (
        <>
          <p>
            <strong>{verified ? 'Verified' : 'Verification failed'}</strong> at{' '}
            {new Date(verification.verifiedAt).toLocaleString()}
          </p>
          {verification.error && <p>{verification.error}</p>}
          <ul>
            {verification.permissions.map((permission) => (
              <li key={permission.action}>
                {permission.allowed ? '✓' : '✗'} {permission.action}
              </li>
            ))}
          </ul>
        </>
      )}
    </>
  )
}

export default App
