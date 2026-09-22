import { Layout } from '../Layout'
import { Stepper } from '../Stepper'

export function Audit() {
  return (
    <Layout>
      <section className="screen">
        <div className="screen-title">
          <h3>Audit in progress</h3>
        </div>
        <Stepper active="Audit" />
        <div className="card">
          <div className="check">
            <span>Audits are not available yet</span>
            <span className="status wait">COMING SOON</span>
          </div>
        </div>
      </section>
    </Layout>
  )
}
