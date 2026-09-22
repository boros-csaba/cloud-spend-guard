import type { FormEvent } from 'react'
import { mailto } from '../contact'
import { Layout } from '../Layout'

export function RequestAccess() {
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = new FormData(event.currentTarget)

    window.location.href = mailto(
      `Free audit request: ${form.get('company')}`,
      [
        `Name: ${form.get('name')}`,
        `Email: ${form.get('email')}`,
        `Company: ${form.get('company')}`,
        `Monthly AWS spend: ${form.get('spend')}`,
        '',
        `${form.get('message')}`,
      ].join('\n'),
    )
  }

  return (
    <Layout showNav>
      <section className="screen">
        <div className="screen-title">
          <div>
            <div className="small">FREE AWS COST AUDIT</div>
            <h3>Request access</h3>
          </div>
        </div>
        <div className="grid2">
          <form className="card" onSubmit={submit}>
            <label className="field">
              <span className="small">Name</span>
              <input className="input" name="name" required />
            </label>
            <label className="field">
              <span className="small">Work email</span>
              <input className="input" name="email" type="email" required />
            </label>
            <label className="field">
              <span className="small">Company</span>
              <input className="input" name="company" required />
            </label>
            <label className="field">
              <span className="small">Monthly AWS spend</span>
              <select className="input" name="spend">
                <option>Under $5,000</option>
                <option>$5,000 – $20,000</option>
                <option>$20,000 – $100,000</option>
                <option>Over $100,000</option>
              </select>
            </label>
            <label className="field">
              <span className="small">Anything I should know? (optional)</span>
              <textarea className="input" name="message" rows={4} />
            </label>
            <button className="btn primary">Request access</button>
          </form>
          <div className="card">
            <h3>What happens next</h3>
            <div className="check">
              <span>I read your request and get back to you personally</span>
            </div>
            <div className="check">
              <span>You get a passwordless login link</span>
            </div>
            <div className="check">
              <span>You connect your AWS account with a read-only role</span>
            </div>
            <div className="check">
              <span>The audit runs, and I review the findings</span>
            </div>
          </div>
        </div>
      </section>
    </Layout>
  )
}
