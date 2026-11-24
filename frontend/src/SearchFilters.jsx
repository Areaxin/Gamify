import { useEffect, useState } from 'react'
import { api } from './api'

// GAM-US13: ExperienceLevel reikšmės (backend RecommendationFilters.cs).
export const experienceLevels = [
  { value: 'Beginner', label: 'Pradedantysis' },
  { value: 'Intermediate', label: 'Vidutinis' },
  { value: 'Experienced', label: 'Patyręs' },
]

export const emptyFilters = { maxPrice: '', playerCount: '', platformIds: null, experience: '' }

const MAX_PRICE = 1000
const MIN_PLAYERS = 1
const MAX_PLAYERS = 8

/** Grąžina klaidos pranešimą arba tuščią eilutę. */
export function validateFilters(filters) {
  const price = filters.maxPrice.trim().replace(',', '.')
  if (price) {
    const value = Number(price)
    if (!Number.isFinite(value)) return 'Didžiausią kainą įveskite skaičiumi.'
    if (value < 0) return 'Didžiausia kaina negali būti neigiama.'
    if (value > MAX_PRICE) return `Didžiausia kaina negali viršyti ${MAX_PRICE} €.`
    if (!/^\d+(\.\d{1,2})?$/.test(price)) return 'Kainą nurodykite ne daugiau kaip dviejų skaitmenų po kablelio tikslumu.'
  }
  const players = filters.playerCount.trim()
  if (players) {
    const value = Number(players)
    if (!Number.isInteger(value) || value < MIN_PLAYERS || value > MAX_PLAYERS) {
      return `Žaidėjų skaičius turi būti nuo ${MIN_PLAYERS} iki ${MAX_PLAYERS}.`
    }
  }
  if (filters.platformIds?.length === 0) return 'Pasirinkite bent vieną platformą.'
  return ''
}

/** Paverčia formos reikšmes į API formatą; null, jei nė vienas filtras nenurodytas. */
export function toRequestFilters(filters) {
  const price = filters.maxPrice.trim().replace(',', '.')
  const players = filters.playerCount.trim()
  const request = {
    maxPriceEur: price ? Number(price) : null,
    playerCount: players ? Number(players) : null,
    platformIds: filters.platformIds,
    experience: filters.experience || null,
  }
  return Object.values(request).some(value => value !== null) ? request : null
}

export default function SearchFilters({ profile, token, filters, onChange, disabled }) {
  const [platforms, setPlatforms] = useState([])

  useEffect(() => {
    const request = new AbortController()
    api('/api/profile/options', { token, signal: request.signal })
      .then(options => {
        if (!request.signal.aborted) setPlatforms(options.platforms)
      })
      .catch(() => {})
    return () => request.abort()
  }, [token])

  // Rodomos visos platformos; pagal nutylėjimą pažymėtos tik profilio platformos.
  const selected = filters.platformIds ?? profile.platformIds

  function change(values) {
    onChange({ ...filters, ...values })
  }

  function togglePlatform(id, checked) {
    const next = checked ? [...selected, id] : selected.filter(value => value !== id)
    const sameAsProfile = next.length === profile.platformIds.length &&
      profile.platformIds.every(value => next.includes(value))
    change({ platformIds: sameAsProfile ? null : next })
  }

  return (
    <fieldset className="search-filters" disabled={disabled}>
      <legend>Filtrai</legend>
      <p className="field-note">Nebūtini. Jei filtras prieštarauja aprašymui, taikomas filtras.</p>
      <div className="filter-grid">
        <label>
          Max kaina (€)
          <input type="text" inputMode="decimal" placeholder="Bet kokia"
            value={filters.maxPrice} onChange={event => change({ maxPrice: event.target.value })} />
        </label>
        <label>
          Žaidėjų skaičius
          <input type="text" inputMode="numeric" placeholder="1–8"
            value={filters.playerCount} onChange={event => change({ playerCount: event.target.value })} />
        </label>
        <label>
          Patirties lygis
          <select value={filters.experience} onChange={event => change({ experience: event.target.value })}>
            <option value="">Nenurodyta</option>
            {experienceLevels.map(level => <option key={level.value} value={level.value}>{level.label}</option>)}
          </select>
        </label>
      </div>
      {platforms.length > 0 && (
        <div className="filter-platforms" role="group" aria-label="Platformos">
          <span>Platformos</span>
          <div className="platforms">
            {platforms.map(platform => (
              <label key={platform.id} className="platform">
                <input type="checkbox" checked={selected.includes(platform.id)}
                  onChange={event => togglePlatform(platform.id, event.target.checked)} />
                {platform.name}
              </label>
            ))}
          </div>
        </div>
      )}
      <button type="button" className="secondary" onClick={() => onChange(emptyFilters)}>Išvalyti filtrus</button>
    </fieldset>
  )
}
