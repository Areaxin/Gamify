import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { api, errorMessage } from './api'
import PlaylistGames from './PlaylistGames'
import StoreButton from './StoreButton'

export default function Recommendations({ profile, token, onUnauthorized, text, setText, forGroup, setForGroup }) {
  const [result, setResult] = useState(null)
  const [error, setError] = useState('')
  const [searching, setSearching] = useState(false)
  const activeRequest = useRef(null)
  const length = Array.from(text).length
  const [templates, setTemplates] = useState([])
  const [templatesLoading, setTemplatesLoading] = useState(false)
  const [templatesError, setTemplatesError] = useState('')
  const [playlistName, setPlaylistName] = useState('')
  const [savingPlaylist, setSavingPlaylist] = useState(false)
  const [playlistMessage, setPlaylistMessage] = useState('')
  const [playlistError, setPlaylistError] = useState('')

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
  }, [token, onUnauthorized])

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

  async function savePlaylist(event) {
    event.preventDefault()
    setPlaylistMessage('')
    setPlaylistError('')
    if (!playlistName.trim() || playlistName.trim().length > 100) {
      setPlaylistError('Įveskite grojaraščio pavadinimą (iki 100 simbolių).')
      return
    }

    setSavingPlaylist(true)
    try {
      const saved = await api('/api/saved-playlists', {
        method: 'POST',
        token,
        body: {
          name: playlistName,
          recommendationToken: result.recommendationToken,
        },
      })
      setPlaylistMessage(`Grojaraštis „${saved.name}“ išsaugotas. Jį rasite profilyje.`)
      setPlaylistName('')
    } catch (error) {
      if (error.status === 401) onUnauthorized()
      else setPlaylistError(errorMessage(error))
    } finally {
      setSavingPlaylist(false)
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
          {searching && <p role="status">Ieškoma žaidimų internete ir tikrinamos kainos…</p>}
        </form>
      )}
      {result && result.games.length === 0 && (
        <div className="empty-playlist" role="status">
          <p>{result.message || 'Žaidimų pagal šią užklausą nerasta.'}</p>
          <p className="field-note">Pabandykite pakeisti nuotaiką, biudžetą arba žaidėjų skaičių.</p>
        </div>
      )}
      {result?.games.length > 0 && (
        <section className="recommendations">
          <h2>Jūsų žaidimų grojaraštis</h2>
          <PlaylistGames games={result.games} renderAction={game => <StoreButton game={game} token={token} />} />
          <form className="save-playlist-form" onSubmit={savePlaylist}>
            <label>
              Išsaugoti grojaraštį pavadinimu
              <input
                required
                maxLength={100}
                value={playlistName}
                onChange={event => setPlaylistName(event.target.value)}
                placeholder="Pavyzdžiui, Žaidimai savaitgaliui"
                disabled={savingPlaylist}
              />
            </label>
            {playlistError && <p className="error" role="alert">{playlistError}</p>}
            {playlistMessage && <p role="status">{playlistMessage}</p>}
            <button type="submit" disabled={savingPlaylist}>
              {savingPlaylist ? 'Išsaugoma…' : 'Išsaugoti grojaraštį'}
            </button>
          </form>
        </section>
      )}
    </section>
  )
}
