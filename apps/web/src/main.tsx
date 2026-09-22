import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter, Route, Routes } from 'react-router'
import App from './App.tsx'
import './index.css'
import { Audit } from './pages/Audit.tsx'
import { Home } from './pages/Home.tsx'
import { Login } from './pages/Login.tsx'
import { RequestAccess } from './pages/RequestAccess.tsx'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<Home />} />
        <Route path="/request-access" element={<RequestAccess />} />
        <Route path="/login" element={<Login />} />
        <Route path="/app" element={<App />} />
        <Route path="/app/connections/:id" element={<App />} />
        <Route path="/app/audits/:id" element={<Audit />} />
      </Routes>
    </BrowserRouter>
  </StrictMode>,
)
