import { useCallback, useEffect, useState } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter, Link, Navigate, Route, Routes, useNavigate } from 'react-router-dom'
import AuthForm from './AuthForm'
import Profile from './Profile'
import Recommendations from './Recommendations'
import { api, errorMessage } from './api'
import './style.css'

const sessionKey = 'gamify.session'

function loadSession() {
  try {
    const session = JSON.parse(sessionStorage.getItem(sessionKey))
    return session?.token && Date.parse(session.expiresAt) > Date.now() ? session : null
  } catch {
    return null
  }
}

function App() {
  const [session, setSession] = useState(loadSession)
  const [user, setUser] = useState(null)
  const [searchText, setSearchText] = useState('')
  const [status, setStatus] = useState('Tikrinamas ryšys su API…')
  const [sessionError, setSessionError] = useState('')
  const [logoutError, setLogoutError] = useState('')
  const [loggingOut, setLoggingOut] = useState(false)
  const [retry, setRetry] = useState(0)
  const navigate = useNavigate()

  const clearSession = useCallback(() => {
    sessionStorage.removeItem(sessionKey)
    setSession(null)
    setUser(null)
    setSearchText('')
    setSessionError('')
    setLogoutError('')
  }, [])

  function login(newSession) {
    sessionStorage.setItem(sessionKey, JSON.stringify(newSession))
    setSession(newSession)
  }

  function reuseQuery(text) {
    setSearchText(text)
    navigate('/')
  }

  async function logout() {
    setLoggingOut(true)
    setLogoutError('')
    try {
      await api('/api/auth/logout', { method: 'POST', token: session.token })
    } catch (error) {
      if (error.status !== 401) {
        setLogoutError(errorMessage(error))
        return
      }
    } finally {
      setLoggingOut(false)
    }
    clearSession()
    navigate('/login', { replace: true })
  }

  useEffect(() => {
    const request = new AbortController()
    api('/health', { signal: request.signal })
      .then(data => {
        if (!request.signal.aborted) setStatus(data?.status === 'ok' ? 'API pasiekiama.' : 'API nepasiekiama.')
      })
      .catch(() => {
        if (!request.signal.aborted) setStatus('API nepasiekiama.')
      })
    return () => request.abort()
  }, [])

  useEffect(() => {
    if (!session) return
    const request = new AbortController()
    const timeout = window.setTimeout(clearSession, Date.parse(session.expiresAt) - Date.now())
    api('/api/profile', { token: session.token, signal: request.signal })
      .then(profile => {
        if (!request.signal.aborted) setUser(profile)
      })
      .catch(error => {
        if (request.signal.aborted) return
        if (error.status === 401) clearSession()
        else setSessionError(errorMessage(error))
      })

    return () => {
      window.clearTimeout(timeout)
      request.abort()
    }
  }, [session, clearSession, retry])

  return (
    <>
      <header className="topbar">
        <Link className="brand" to="/">Gamify</Link>
        {user && (
          <nav aria-label="Pagrindinis meniu">
            <Link to="/">Pradžia</Link>
            <Link to="/profile">Profilis</Link>
            <button type="button" className="secondary" onClick={logout} disabled={loggingOut}>Atsijungti</button>
          </nav>
        )}
      </header>

      <main>
        {logoutError && <p className="error" role="alert">{logoutError}</p>}
        {session && !user ? (
          sessionError ? (
            <section className="panel">
              <p className="error" role="alert">{sessionError}</p>
              <button type="button" onClick={() => {
                setSessionError('')
                setRetry(value => value + 1)
              }}>Bandyti dar kartą</button>
            </section>
          ) : <p role="status">Tikrinama sesija…</p>
        ) : (
          <Routes>
            <Route path="/" element={user ? (
              <Recommendations profile={user} token={session.token} onUnauthorized={clearSession}
                text={searchText} setText={setSearchText} />
            ) : <Navigate to="/login" replace />} />
            <Route path="/profile" element={user ? (
              <Profile token={session.token} onUnauthorized={clearSession} onSave={setUser} onReuseQuery={reuseQuery} />
            ) : <Navigate to="/login" replace />} />
            <Route path="/login" element={user
              ? <Navigate to="/" replace />
              : <AuthForm key="login" onLogin={login} />} />
            <Route path="/register" element={user
              ? <Navigate to="/" replace />
              : <AuthForm key="register" register />} />
            <Route path="*" element={<Navigate to="/" replace />} />
          </Routes>
        )}
      </main>

      <footer role="status">{status}</footer>
    </>
  )
}

createRoot(document.getElementById('root')).render(
  <BrowserRouter><App /></BrowserRouter>,
)
