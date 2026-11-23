import { useState } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { api, errorMessage } from './api'

export default function AuthForm({ register = false, onLogin }) {
  const [error, setError] = useState('')
  const [saving, setSaving] = useState(false)
  const location = useLocation()
  const navigate = useNavigate()

  async function submit(event) {
    event.preventDefault()
    setError('')
    setSaving(true)
    const body = Object.fromEntries(new FormData(event.currentTarget))

    try {
      if (register) {
        await api('/api/auth/register', { method: 'POST', body })
        navigate('/login', { replace: true, state: { registered: true, email: body.email.trim() } })
      } else {
        const session = await api('/api/auth/login', { method: 'POST', body })
        onLogin(session)
        navigate('/', { replace: true })
      }
    } catch (error) {
      setError(errorMessage(error))
    } finally {
      setSaving(false)
    }
  }

  return (
    <section className="panel auth-panel">
      <h1>{register ? 'Registracija' : 'Prisijungimas'}</h1>
      {!register && location.state?.registered && (
        <p role="status">Paskyra sukurta. Dabar galite prisijungti.</p>
      )}
      <form onSubmit={submit} noValidate>
        {register && (
          <label>
            Vardas
            <input name="name" autoComplete="name" required />
          </label>
        )}
        <label>
          El. paštas
          <input name="email" type="email" autoComplete="email" required defaultValue={location.state?.email ?? ''} />
        </label>
        <label>
          Slaptažodis
          <input name="password" type="password" autoComplete={register ? 'new-password' : 'current-password'}
            minLength={register ? 8 : undefined} required />
        </label>
        {error && <p className="error" role="alert">{error}</p>}
        <button type="submit" disabled={saving}>{register ? 'Registruotis' : 'Prisijungti'}</button>
      </form>
      <p>{register ? <Link to="/login">Jau turiu paskyrą</Link> : <Link to="/register">Sukurti paskyrą</Link>}</p>
    </section>
  )
}
