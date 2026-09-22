import { useState, type FormEvent } from 'react'
import { Link } from 'react-router'
import { Layout } from '../Layout'

export function Login() {
  const [sentTo, setSentTo] = useState<string | null>(null)

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSentTo(`${new FormData(event.currentTarget).get('email')}`)
  }

  return (
    <Layout showNav>
      <section className="screen" style={{ maxWidth: 480 }}>
        <div className="screen-title">
          <h3>Sign in</h3>
        </div>
        <div className="card">
          {sentTo ? (
            <>
              <h3>Check your inbox</h3>
              <p className="muted">I've sent a login link to {sentTo}.</p>
              <Link to="/app" className="btn">
                Continue (demo)
              </Link>
            </>
          ) : (
            <form onSubmit={submit}>
              <label className="field">
                <span className="small">Email</span>
                <input className="input" name="email" type="email" required />
              </label>
              <button className="btn primary">Send login link</button>
            </form>
          )}
        </div>
      </section>
    </Layout>
  )
}
