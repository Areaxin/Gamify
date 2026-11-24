import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { api, errorMessage } from './api'

const euro = new Intl.NumberFormat('lt-LT', { style: 'currency', currency: 'EUR' })

export default function Recommendations({ profile, token, onUnauthorized }) {
  const [text, setText] = useState('')
  const [forGroup, setForGroup] = useState(false)
  const [result, setResult] = useState(null)
  const [error, setError] = useState('')
  const [searching, setSearching] = useState(false)
  const activeRequest = useRef(null)
  const length = Array.from(text).length
  const [templates, setTemplates] = useState([])
  const [templatesLoading, setTemplatesLoading] = useState(false)
  const [templatesError, setTemplatesError] = useState('')

  useEffect(() => () => activeRequest.current?.abort(), [])
  useEffect(() => {
    const request = new AbortController()

    async function loadTemplates() {
      setTemplatesLoading(true)

      try {
        const data = await api('/api/query-templates', {
          token,
          signal: request.signal,
        })

        setTemplates(data)
      } catch (error) {
        if (request.signal.aborted) return

        if (error.status === 401) onUnauthorized()
        else setTemplatesError(errorMessage(error))
      } finally {
        if (!request.signal.aborted) setTemplatesLoading(false)
      }
    }

    loadTemplates()

    return () => request.abort()
  }, [token])

  async function search(event) {
    event.preventDefault()
    setError('')
    setResult(null)
    if (!text.trim()) {
      setError('Aprašykite, kokio žaidimo ieškote.')
      return
    }
    if (length > 1000) {
      setError('Aprašymas negali viršyti 1000 simbolių.')
      return
    }

    const request = new AbortController()
    activeRequest.current = request
    setSearching(true)
    try {
      setResult(await api('/api/recommendations', {
        method: 'POST',
        token,
        body: { text, forGroup },
        signal: request.signal,
      }))
    } catch (error) {
      if (request.signal.aborted) return
      if (error.status === 401) onUnauthorized()
      else setError(errorMessage(error))
    } finally {
      if (!request.signal.aborted) setSearching(false)
    }
  }

  return (
    <section className="panel">
      <h1>Sveiki, {profile.name}!</h1>
      {!profile.birthDate || !profile.platformIds.length ? (
        <p>Norėdami gauti rekomendacijas, <Link to="/profile">profilyje</Link> nurodykite gimimo datą ir turimas platformas.</p>
      ) : (
        <form onSubmit={search}>
          <div className="query-templates">
            <p>Paruošti paieškos šablonai:</p>

            {templatesLoading && <p>Įkeliami šablonai...</p>}

            {templatesError && (
                <p className="error">{templatesError}</p>
            )}

            {templates.map(template => (
                <button
                    key={template.id}
                    type="button"
                    disabled={searching}
                    onClick={() => {
                      setText(template.text)
                      setError('')
                      setResult(null)
                    }}
                >
                  {template.title}
                </button>
            ))}
          </div>
          <label>
            Kokio žaidimo ieškote?
            <textarea rows={4} value={text} aria-describedby="description-length" placeholder="Ramus žaidimas vakarui su draugais, iki 20 €, pradedantiesiems"
              disabled={searching} onChange={event => setText(event.target.value)} />
          </label>
          <p id="description-length" className="field-note">{length} / 1000 simbolių</p>
          {profile.groupBirthDates.length > 0 && (
            <label className="platform">
              <input type="checkbox" checked={forGroup} disabled={searching}
                onChange={event => { setForGroup(event.target.checked); setResult(null) }} />
              Ieškoti žaidimų grupei
            </label>
          )}
          {error && <p className="error" role="alert">{error}</p>}
          <button type="submit" disabled={searching}>{searching ? 'Ieškoma…' : 'Ieškoti žaidimų'}</button>
        </form>
      )}
      {result?.message && <p role="status">{result.message}</p>}
      {result?.games.length > 0 && (
        <section className="recommendations">
          <h2>Rasti žaidimai</h2>
          <p className="field-note">Katalogo kainos orientacinės.</p>
          <ul>
            {result.games.map(game => (
              <li key={game.id}>
                <strong>{game.title}</strong>
                <span>{game.priceCents ? euro.format(game.priceCents / 100) : 'Nemokamas'} · {game.platforms.join(', ')}</span>
                <span className="field-note">
                  {game.maxPlayers === 1 ? '1 žaidėjas' : `Iki ${game.maxPlayers} žaidėjų`}
                  {game.pegi != null && ` · PEGI ${game.pegi}`}
                  {game.esrb && ` · ESRB ${game.esrb}`}
                </span>
              </li>
            ))}
          </ul>
        </section>
      )}
    </section>
  )
}
