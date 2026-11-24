import { useEffect, useState } from 'react'
import { api, errorMessage } from './api'
import { experienceLevels } from './SearchFilters'
import GroupManagement from './GroupManagement'

function ratingText(rating) {
  return [rating.pegi && `PEGI ${rating.pegi}`, `ESRB ${rating.esrb}`].filter(Boolean).join(' / ')
}

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
    const budgetText = String(profile.preferredBudgetEur ?? '').trim().replace(',', '.')
    let preferredBudgetEur = null
    if (budgetText) {
      preferredBudgetEur = Number(budgetText)
      if (!/^\d+(\.\d{1,2})?$/.test(budgetText) || preferredBudgetEur > 1000) {
        setError('Pageidaujamas biudžetas turi būti nuo 0 iki 1000 € (iki 2 skaitmenų po kablelio).')
        return
      }
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
          preferredBudgetEur,
          preferredExperience: profile.preferredExperience || null,
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
    <section className="panel profile-panel">
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
          <fieldset className="profile-preferences">
            <legend>Rekomendacijų pageidavimai</legend>
            <p className="field-note">Šie pageidavimai bus taikomi ir grupės paieškoje.</p>
            <label>
              Didžiausias biudžetas (€)
              <input type="text" inputMode="decimal" placeholder="Nenurodytas"
                value={profile.preferredBudgetEur ?? ''}
                onChange={event => change({ preferredBudgetEur: event.target.value })} />
            </label>
            <label>
              Patirties lygis
              <select value={profile.preferredExperience ?? ''}
                onChange={event => change({ preferredExperience: event.target.value || null })}>
                <option value="">Nenurodyta</option>
                {experienceLevels.map(level => (
                  <option key={level.value} value={level.value}>{level.label}</option>
                ))}
              </select>
            </label>
          </fieldset>
          {saved && <p role="status">Profilis išsaugotas.</p>}
          <button type="submit" disabled={saving}>Išsaugoti</button>
        </form>
      )}
      {profile && <GroupManagement token={token} onUnauthorized={onUnauthorized} />}
    </section>
  )
}
