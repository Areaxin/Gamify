import { useEffect, useState } from 'react'
import { api, errorMessage } from './api'

export default function Profile({ token, onUnauthorized, onSave }) {
  const [profile, setProfile] = useState(null)
  const [options, setOptions] = useState(null)
  const [error, setError] = useState('')
  const [saved, setSaved] = useState(false)
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    const request = new AbortController()
    const config = { token, signal: request.signal }
    Promise.all([api('/api/profile', config), api('/api/profile/options', config)])
      .then(([profile, options]) => {
        if (request.signal.aborted) return
        setProfile(profile)
        setOptions(options)
      })
      .catch(error => {
        if (request.signal.aborted) return
        if (error.status === 401) onUnauthorized()
        else setError(errorMessage(error))
      })
    return () => request.abort()
  }, [token, onUnauthorized])

  function change(values) {
    setProfile(current => ({ ...current, ...values }))
    setSaved(false)
    setError('')
  }

  function togglePlatform(id, checked) {
    const platformIds = checked
      ? [...profile.platformIds, id]
      : profile.platformIds.filter(value => value !== id)
    change({ platformIds })
  }

  async function submit(event) {
    event.preventDefault()
    setError('')
    setSaved(false)
    setSaving(true)
    try {
      const updated = await api('/api/profile', {
        method: 'PUT',
        token,
        body: { name: profile.name, platformIds: profile.platformIds, pcLevel: profile.pcLevel },
      })
      setProfile(updated)
      onSave(updated)
      setSaved(true)
    } catch (error) {
      if (error.status === 401) onUnauthorized()
      else setError(errorMessage(error))
    } finally {
      setSaving(false)
    }
  }

  const hasPc = options?.platforms.some(platform => platform.name === 'PC' && profile.platformIds.includes(platform.id))

  return (
    <section className="panel">
      <h1>Mano profilis</h1>
      {error && <p className="error" role="alert">{error}</p>}
      {!profile && !error && <p role="status">Įkeliamas profilis…</p>}
      {profile && options && (
        <form onSubmit={submit} noValidate>
          <label>
            Vardas
            <input name="name" autoComplete="name" required value={profile.name}
              onChange={event => change({ name: event.target.value })} />
          </label>
          <label>
            El. paštas
            <input value={profile.email} type="email" readOnly />
          </label>
          <fieldset>
            <legend>Turimos platformos</legend>
            <div className="platforms">
              {options.platforms.map(platform => (
                <label key={platform.id} className="platform">
                  <input type="checkbox" checked={profile.platformIds.includes(platform.id)}
                    onChange={event => togglePlatform(platform.id, event.target.checked)} />
                  {platform.name}
                </label>
              ))}
            </div>
          </fieldset>
          {hasPc && (
            <label>
              PC specifikacijų lygis
              <select required value={profile.pcLevel ?? ''} onChange={event => change({ pcLevel: event.target.value })}>
                <option value="">Pasirinkite lygį</option>
                {options.pcLevels.map(level => <option key={level}>{level}</option>)}
              </select>
            </label>
          )}
          {saved && <p role="status">Profilis išsaugotas.</p>}
          <button type="submit" disabled={saving}>Išsaugoti</button>
        </form>
      )}
    </section>
  )
}
