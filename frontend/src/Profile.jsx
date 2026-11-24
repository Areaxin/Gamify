import { useEffect, useState } from 'react'
import { api, errorMessage } from './api'
import QueryHistory from './QueryHistory'
import SavedPlaylists from './SavedPlaylists'

function ratingText(rating) {
  return [rating.pegi && `PEGI ${rating.pegi}`, `ESRB ${rating.esrb}`].filter(Boolean).join(' / ')
}

export default function Profile({ token, onUnauthorized, onSave, onReuseQuery }) {
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

  function changeGroupDate(index, date) {
    change({ groupBirthDates: profile.groupBirthDates.map((value, position) => position === index ? date : value) })
  }

  async function submit(event) {
    event.preventDefault()
    setError('')
    setSaved(false)
    if (profile.groupBirthDates.some(date => !date)) {
      setError('Įveskite kiekvieno pridėto grupės nario gimimo datą.')
      return
    }
    setSaving(true)
    try {
      const updated = await api('/api/profile', {
        method: 'PUT',
        token,
        body: {
          name: profile.name,
          platformIds: profile.platformIds,
          pcLevel: profile.pcLevel,
          birthDate: profile.birthDate || null,
          groupBirthDates: profile.groupBirthDates,
        },
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
        <>
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
          <label>
            Gimimo data
            <input type="date" autoComplete="bday" value={profile.birthDate ?? ''}
              readOnly={Boolean(profile.ageRating)} aria-describedby="birth-date-note"
              onChange={event => change({ birthDate: event.target.value })} />
          </label>
          <p className="field-note" id="birth-date-note">
            {profile.ageRating
              ? `Leidžiamas amžiaus reitingas iki ${ratingText(profile.ageRating)}.`
              : 'Gimimo data reikalinga rekomendacijoms. Išsaugojus jos pakeisti nebegalėsite.'}
          </p>
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
          <fieldset className="group-fields">
            <legend>Grupės nariai</legend>
            {profile.groupBirthDates.map((date, index) => (
              <div className="group-member" key={index}>
                <label>
                  {index + 1}. nario gimimo data
                  <input type="date" value={date} onChange={event => changeGroupDate(index, event.target.value)} />
                </label>
                <button type="button" className="secondary" aria-label={`Pašalinti ${index + 1} narį`}
                  onClick={() => change({ groupBirthDates: profile.groupBirthDates.filter((_, position) => position !== index) })}>
                  Pašalinti
                </button>
              </div>
            ))}
            {profile.groupAgeRating && <p className="field-note">Grupės amžiaus reitingas iki {ratingText(profile.groupAgeRating)}.</p>}
            <button type="button" className="secondary"
              disabled={!profile.birthDate || profile.groupBirthDates.length >= options.maxGroupMembers}
              onClick={() => change({ groupBirthDates: [...profile.groupBirthDates, ''] })}>
              Pridėti narį
            </button>
          </fieldset>
          {saved && <p role="status">Profilis išsaugotas.</p>}
          <button type="submit" disabled={saving}>Išsaugoti</button>
        </form>
        <SavedPlaylists token={token} onUnauthorized={onUnauthorized} />
        </>
      )}
      <QueryHistory token={token} onUnauthorized={onUnauthorized} onReuse={onReuseQuery} />
    </section>
  )
}
