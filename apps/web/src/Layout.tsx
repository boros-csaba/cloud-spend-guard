import type { ReactNode } from 'react'
import { Link } from 'react-router'

export function Layout({ children, showNav = false }: { children: ReactNode; showNav?: boolean }) {
  return (
    <>
      <header className="shell topbar">
        <Link to="/" className="brand">
          Cloud Spend Guard<span className="small">by Csaba Boros</span>
        </Link>
        {showNav && (
          <>
            <nav className="nav">
              <a href="/#how-it-works">How it works</a>
              <a href="/#sample">Sample findings</a>
              <a href="/#about">About</a>
              <a href="/#contract-work">Contract work</a>
            </nav>
            <div className="actions">
              <Link to="/login" className="btn">
                Sign in
              </Link>
              <Link to="/request-access" className="btn primary">
                Request a free audit
              </Link>
            </div>
          </>
        )}
      </header>

      <main className="shell">{children}</main>

      <footer>
        <div className="shell">Cloud Spend Guard · AWS cost audits and contract engineering · Csaba Boros</div>
      </footer>
    </>
  )
}
