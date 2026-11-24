import { useCallback, useEffect, useState } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter, Link, Navigate, Route, Routes, useLocation, useNavigate } from 'react-router-dom'
import { Bookmark, Clock3, Gamepad2, LogOut, Search, UserRound } from 'lucide-react'
import AuthForm from './AuthForm'
import Profile from './Profile'
import QueryHistory from './QueryHistory'
import SavedPlaylists from './SavedPlaylists'
import Recommendations from './Recommendations'
import { api, errorMessage } from './api'
import '@fontsource-variable/manrope'
import './style.css'

const sessionKey = 'gamify.session'

const navigation = [
  { to: '/', label: 'Paieška', icon: Search },
  { to: '/playlists', label: 'Išsaugoti grojaraščiai', icon: Bookmark },
  { to: '/history', label: 'Užklausų istorija', icon: Clock3 },
  { to: '/profile', label: 'Mano profilis', icon: UserRound },
]

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
  const [forGroup, setForGroup] = useState(false)
  const [sessionError, setSessionError] = useState('')
  const [logoutError, setLogoutError] = useState('')
  const [loggingOut, setLoggingOut] = useState(false)
  const [retry, setRetry] = useState(0)
  const navigate = useNavigate()
  const location = useLocation()

  useEffect(() => {
    window.scrollTo(0, 0)
  }, [location.pathname])

  const clearSession = useCallback(() => {
    sessionStorage.removeItem(sessionKey)
    setSession(null)
    setUser(null)
    setSearchText('')
    setForGroup(false)
    setSessionError('')
    setLogoutError('')
  }, [])

  function login(newSession) {
    sessionStorage.setItem(sessionKey, JSON.stringify(newSession))
    setSession(newSession)
  }

  function reuseQuery(entry) {
    setSearchText(entry.text)
    setForGroup(entry.forGroup && user.groupBirthDates.length > 0)
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
    <div className={user ? 'app-shell' : 'app-shell guest-shell'}>
      <header className="sidebar">
        <Link className="brand" to="/">
          <span className="brand-icon"><Gamepad2 size={23} aria-hidden="true" /></span>
          Gamify
        </Link>
        {user && (
          <>
            <nav className="sidebar-nav" aria-label="Pagrindinis meniu">
              {navigation.map(({ to, label, icon: Icon }) => {
                const current = location.pathname === to
                return (
                  <Link key={to} to={to} aria-current={current ? 'page' : undefined}>
                    <Icon size={18} aria-hidden="true" />
                    {label}
                  </Link>
                )
              })}
            </nav>
            <div className="sidebar-account">
              <div className="account-name">
                <span className="avatar" aria-hidden="true">{Array.from(user.name)[0]?.toLocaleUpperCase('lt')}</span>
                <strong>{user.name}</strong>
              </div>
              <button type="button" className="logout-button" onClick={logout} disabled={loggingOut}>
                <LogOut size={17} aria-hidden="true" /> Atsijungti
              </button>
            </div>
          </>
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
                text={searchText} setText={setSearchText} forGroup={forGroup && user.groupBirthDates.length > 0}
                setForGroup={setForGroup} />
            ) : <Navigate to="/login" replace />} />
            <Route path="/profile" element={user ? (
              <Profile token={session.token} onUnauthorized={clearSession} onSave={setUser} />
            ) : <Navigate to="/login" replace />} />
            <Route path="/playlists" element={user ? (
              <SavedPlaylists token={session.token} onUnauthorized={clearSession} />
            ) : <Navigate to="/login" replace />} />
            <Route path="/history" element={user ? (
              <QueryHistory token={session.token} onUnauthorized={clearSession} onReuse={reuseQuery} />
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

    </div>
  )
}

createRoot(document.getElementById('root')).render(
  <BrowserRouter><App /></BrowserRouter>,
)
