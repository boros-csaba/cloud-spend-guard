import { useEffect, useState, type ReactNode } from 'react'
import { useNavigate, useParams } from 'react-router'
import { Layout } from './Layout'
import { Stepper } from './Stepper'

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

function App() {
  const { id: connectionId } = useParams()
  const navigate = useNavigate()
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
  }, [connectionId])

  async function createConnection() {
    setBusy(true)

    try {
      const created = await request<Connection>('/connections', { method: 'POST' })
      navigate(`/app/connections/${created.id}`)
    } catch (error) {
      setError((error as Error).message)
    } finally {
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
      <ConnectScreen active="Connect AWS">
        <div className="card">
          <h3>Create a Connection</h3>
          <p className="muted">I'll issue an External ID for the read-only Auditor Role you create in your AWS account.</p>
          <button className="btn primary" onClick={createConnection} disabled={busy}>
            Create Connection
          </button>
          {error && <p className="error">{error}</p>}
        </div>
      </ConnectScreen>
    )
  }

  if (!connection) {
    return (
      <ConnectScreen active="Connect AWS">
        <div className="card">
          {error ? <p className="error">{error}</p> : <span className="status run">LOADING…</span>}
        </div>
      </ConnectScreen>
    )
  }

  const verification = connection.verification
  const verified = verification?.status === 'verified'

  return (
    <ConnectScreen active={verification ? 'Verification' : 'Connect AWS'}>
      <div className="grid2">
        <div className="card">
          <h3>1. Create the Auditor Role</h3>
          <p className="muted">
            In the AWS account you want to audit, create an IAM role named <code>{connection.roleName}</code> with
            this trust policy (External ID <code>{connection.externalId}</code>):
          </p>
          <pre className="code">{connection.trustPolicy}</pre>
          <p className="muted">and attach this permissions policy:</p>
          <pre className="code">{connection.permissionsPolicy}</pre>
        </div>

        <div className="card">
          <h3>2. Verify it</h3>
          <div className="field">
            <input
              className="input"
              value={roleArn}
              onChange={(event) => setRoleArn(event.target.value)}
              placeholder={`arn:aws:iam::123456789012:role/${connection.roleName}`}
              readOnly={verified}
            />
          </div>
          <button className="btn primary" onClick={verify} disabled={busy || !roleArn}>
            Verify
          </button>
          {error && <p className="error">{error}</p>}

          {busy && (
            <div className="check">
              <span>Assuming the Auditor Role and checking permissions</span>
              <span className="status run">CHECKING…</span>
            </div>
          )}

          {verification && !busy && (
            <>
              <div className="check">
                <span>
                  <b>{verified ? 'Verified' : 'Verification failed'}</b>
                  <br />
                  <span className="small">{new Date(verification.verifiedAt).toLocaleString()}</span>
                </span>
                <span className={verified ? 'status ok' : 'status fail'}>{verified ? 'PASSED' : 'FAILED'}</span>
              </div>
              {verification.error && <p className="error">{verification.error}</p>}
              {verification.permissions.map((permission) => (
                <div key={permission.action} className="check">
                  <code>{permission.action}</code>
                  <span className={permission.allowed ? 'status ok' : 'status fail'}>
                    {permission.allowed ? 'ALLOWED' : 'DENIED'}
                  </span>
                </div>
              ))}
            </>
          )}
        </div>
      </div>
    </ConnectScreen>
  )
}

function ConnectScreen({ active, children }: { active: 'Connect AWS' | 'Verification'; children: ReactNode }) {
  return (
    <Layout>
      <section className="screen">
        <div className="screen-title">
          <h3>Connect your AWS account</h3>
        </div>
        <Stepper active={active} />
        {children}
      </section>
    </Layout>
  )
}

export default App
