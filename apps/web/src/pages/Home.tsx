import { Link } from 'react-router'
import { mailto } from '../contact'
import { Layout } from '../Layout'

const sampleFindings = [
  {
    service: 'EC2',
    title: 'Oversized production instances',
    description: '3 × m6i.4xlarge instances show consistently low utilization over the last 30 days.',
    savings: '$1,460/mo',
    confidence: 'High confidence',
  },
  {
    service: 'RDS',
    title: 'Underutilized database instances',
    description: 'CPU and connection metrics point to instances that can likely be downsized.',
    savings: '$920/mo',
    confidence: 'Medium confidence',
  },
  {
    service: 'NETWORK',
    title: 'Unused NAT gateways',
    description: 'Two gateways processed almost no data over the observation window.',
    savings: '$340/mo',
    confidence: 'High confidence',
  },
]

export function Home() {
  return (
    <Layout showNav>
      <section className="hero">
        <div>
          <div className="eyebrow">AWS cost audits · Contract engineering</div>
          <h1>I'll find what AWS is costing you more than it should.</h1>
          <p className="lead">
            I'm Csaba Boros, a senior software and cloud engineer. I run an automated audit of your AWS account, then
            review the findings myself, so you know which savings are safe and worth acting on. The first audit is
            free.
          </p>
          <div className="cta">
            <Link to="/request-access" className="btn primary">
              Request a free audit
            </Link>
            <a href="#sample" className="btn">
              See a sample report
            </a>
          </div>
        </div>
        <div className="card">
          <span className="badge">Sample audit</span>
          <div className="small" style={{ marginTop: 18 }}>
            Potential monthly savings
          </div>
          <div className="metric green">$8,420</div>
          <div className="muted">19.4% of current AWS spend</div>
          <div className="grid2" style={{ margin: '24px 0 20px' }}>
            <div>
              <div className="small">Findings</div>
              <div className="kpi">23</div>
            </div>
            <div>
              <div className="small">High impact</div>
              <div className="kpi">4</div>
            </div>
          </div>
          <div className="bar">
            <span style={{ width: '63%' }} />
          </div>
        </div>
      </section>

      <section className="section" id="how-it-works">
        <h2>How it works</h2>
        <div className="grid4">
          <div className="card">
            <div className="small">01</div>
            <h3>Request access</h3>
            <div className="muted">
              Tell me a little about your account. I read every request myself and send you a passwordless login link.
            </div>
          </div>
          <div className="card">
            <div className="small">02</div>
            <h3>Connect AWS</h3>
            <div className="muted">
              Create a read-only IAM role from the template I give you. I can't change anything or read your
              application data.
            </div>
          </div>
          <div className="card">
            <div className="small">03</div>
            <h3>Watch the audit run</h3>
            <div className="muted">I verify the role's permissions, then you can follow every check as it runs.</div>
          </div>
          <div className="card">
            <div className="small">04</div>
            <h3>Review the findings</h3>
            <div className="muted">
              You get prioritized findings with estimated savings, and we can go through them together on a call.
            </div>
          </div>
        </div>
      </section>

      <section className="section" id="sample">
        <div className="screen-title">
          <h2>What you get</h2>
          <span className="badge">Sample data</span>
        </div>
        <div className="card">
          {sampleFindings.map((finding) => (
            <div key={finding.title} className="finding">
              <div>
                <span className="badge">{finding.service}</span>
                <h3>{finding.title}</h3>
                <div className="muted">{finding.description}</div>
              </div>
              <div>
                <div className="money">{finding.savings}</div>
                <div className="small">{finding.confidence}</div>
              </div>
            </div>
          ))}
        </div>
      </section>

      <section className="section" id="about">
        <div className="card personal">
          <div className="avatar">CB</div>
          <div>
            <div className="eyebrow">Who's behind it</div>
            <h2 style={{ margin: '8px 0 10px' }}>One engineer, not a sales team.</h2>
            <p className="muted">
              I built Cloud Spend Guard to automate the repetitive part of an AWS cost review. The tool finds the
              candidates. I check the important ones by hand, because whether a change is safe depends on how your
              system actually works.
            </p>
            <p className="muted">[placeholder: years of experience, highlights, location and time zone]</p>
          </div>
        </div>
      </section>

      <section className="section" id="contract-work">
        <h2>I also take on contract work</h2>
        <p className="lead">If you want help beyond the audit, I can join your team for a project or longer.</p>
        <div className="grid3" style={{ marginTop: 24 }}>
          <div className="card">
            <h3>Cost remediation</h3>
            <div className="muted">
              I implement the fixes from your audit: rightsizing, cleanup, commitments and architecture changes.
            </div>
          </div>
          <div className="card">
            <h3>AWS architecture and infrastructure</h3>
            <div className="muted">Serverless designs, infrastructure as code with CDK, and least-privilege IAM.</div>
          </div>
          <div className="card">
            <h3>Backend engineering</h3>
            <div className="muted">.NET APIs and services, built to run reliably and cheaply on AWS.</div>
          </div>
        </div>
        <div className="cta">
          <a href={mailto('Contract work')} className="btn">
            Get in touch
          </a>
        </div>
      </section>

      <section className="section">
        <div className="card">
          <h2>Want me to look at your AWS bill?</h2>
          <p className="muted">
            Request a free audit, or book a call if you'd rather talk it through first.
          </p>
          <div className="cta">
            <Link to="/request-access" className="btn primary">
              Request a free audit
            </Link>
            <a href={mailto('Book a call')} className="btn">
              Book a call
            </a>
          </div>
        </div>
      </section>
    </Layout>
  )
}
