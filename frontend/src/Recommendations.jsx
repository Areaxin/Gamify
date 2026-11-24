import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { Bookmark, ChevronDown, Gamepad2, Search, SlidersHorizontal } from 'lucide-react'
import { api, errorMessage } from './api'
import PlaylistGames from './PlaylistGames'
import StoreButton from './StoreButton'
import SearchFilters, { emptyFilters, toRequestFilters, validateFilters } from './SearchFilters'

export default function Recommendations({ profile, token, onUnauthorized, text, setText, forGroup, setForGroup }) {
  const [filters, setFilters] = useState(emptyFilters)
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
  const [showSave, setShowSave] = useState(false)
  const [sortOrder, setSortOrder] = useState('match')

  const games = result?.games ?? []
  const sortedGames = sortOrder === 'price'
    ? [...games].sort((a, b) => (a.priceCents ?? Infinity) - (b.priceCents ?? Infinity))
    : games
  const filterCount = Object.values(toRequestFilters(filters) ?? {}).filter(value => value !== null).length

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
    setShowSave(false)
    setPlaylistMessage('')
    setPlaylistError('')
    if (!text.trim()) {
      setError('Aprašykite, kokio žaidimo ieškote.')
      return
    }
    if (length > 1000) {
      setError('Aprašymas negali viršyti 1000 simbolių.')
      return
    }
    const filterError = validateFilters(filters)
    if (filterError) {
      setError(filterError)
      return
    }

    const request = new AbortController()
    activeRequest.current = request
    setSearching(true)
    try {
      setResult(await api('/api/recommendations', {
        method: 'POST',
        token,
        body: { text, forGroup, filters: toRequestFilters(filters) },
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
      setPlaylistMessage(`Grojaraštis „${saved.name}“ išsaugotas.`)
      setPlaylistName('')
      setShowSave(false)
    } catch (error) {
      if (error.status === 401) onUnauthorized()
      else setPlaylistError(errorMessage(error))
    } finally {
      setSavingPlaylist(false)
    }
  }

  return (
    <section className="search-page">
      <header className="page-heading">
        <h1>Rekomendacijos tau</h1>
        <p>Aprašyk žaidimą, kurio ieškai. Pasiūlymus pritaikysime tavo platformoms.</p>
      </header>
      {!profile.birthDate || !profile.platformIds.length ? (
        <p className="panel">Norėdami gauti rekomendacijas, <Link to="/profile">profilyje</Link> nurodykite gimimo datą ir turimas platformas.</p>
      ) : (
        <form className="panel search-form" onSubmit={search} noValidate>
          <label>
            Kokio žaidimo ieškai?
            <textarea rows={3} value={text} aria-describedby="description-length" placeholder="Ramus žaidimas vakarui su draugais, iki 20 €, pradedantiesiems"
              disabled={searching} onChange={event => setText(event.target.value)} />
          </label>
          <div className="search-actions">
            <details className="filter-menu">
              <summary>
                <SlidersHorizontal size={17} aria-hidden="true" />
                Filtrai{filterCount ? ` (${filterCount})` : ''}
                <ChevronDown size={15} aria-hidden="true" />
              </summary>
              <SearchFilters profile={profile} token={token} filters={filters} disabled={searching}
                onChange={values => { setFilters(values); setError('') }} />
            </details>
            <span id="description-length" className="field-note">{length} / 1000</span>
            <button type="submit" className="icon-button" disabled={searching}>
              <Search size={17} aria-hidden="true" /> {searching ? 'Ieškoma…' : 'Ieškoti žaidimų'}
            </button>
          </div>
          {profile.groupBirthDates.length > 0 && (
            <label className="platform">
              <input type="checkbox" checked={forGroup} disabled={searching}
                onChange={event => { setForGroup(event.target.checked); setResult(null) }} />
              Ieškoti žaidimų grupei
            </label>
          )}
          {error && <p className="error" role="alert">{error}</p>}
          <div className="query-templates">
            <span className="field-note">Paieškos šablonai</span>
            {templatesLoading && <span className="field-note">Įkeliami…</span>}
            {templatesError && <p className="error" role="alert">{templatesError}</p>}
            <div className="template-list">
              {templates.map(template => (
                <button key={template.id} type="button" className="secondary" disabled={searching}
                  onClick={() => { setText(template.text); setError(''); setResult(null) }}>
                  {template.title}
                </button>
              ))}
            </div>
          </div>
        </form>
      )}
      {searching && (
        <div className="search-progress" role="status">
          <div className="loading-emblem" aria-hidden="true">
            <span className="loading-ring" />
            <Gamepad2 size={45} strokeWidth={1.65} />
          </div>
          <strong>Ieškome žaidimų</strong>
          <p>Tikriname parduotuves, kainas ir platformas.</p>
          <span className="loading-dots" aria-hidden="true"><i /><i /><i /></span>
        </div>
      )}
      {result && result.games.length === 0 && (
        <div className="empty-playlist" role="status">
          <p>{result.message || 'Žaidimų pagal šią užklausą nerasta.'}</p>
          <p className="field-note">Pabandykite pakeisti nuotaiką, biudžetą arba žaidėjų skaičių.</p>
        </div>
      )}
      {games.length > 0 && (
        <section className="recommendations">
          <div className="results-header">
            <h2>Rasti žaidimai <span>{games.length}</span></h2>
            <div className="results-actions">
              <label className="sort-label">
                Rikiuoti
                <select value={sortOrder} onChange={event => setSortOrder(event.target.value)}>
                  <option value="match">Pagal atitikimą</option>
                  <option value="price">Pigiausi pirmiausia</option>
                </select>
              </label>
              <button type="button" className="icon-button" aria-expanded={showSave}
                onClick={() => setShowSave(value => !value)}>
                <Bookmark size={17} aria-hidden="true" /> {showSave ? 'Atšaukti išsaugojimą' : 'Išsaugoti grojaraštį'}
              </button>
            </div>
          </div>
          {playlistMessage && <p className="playlist-message" role="status">{playlistMessage} <Link to="/playlists">Išsaugoti grojaraščiai</Link></p>}
          {showSave && <form className="panel save-playlist-form" onSubmit={savePlaylist}>
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
            <button type="submit" disabled={savingPlaylist}>
              {savingPlaylist ? 'Išsaugoma…' : 'Išsaugoti grojaraštį'}
            </button>
          </form>}
          <PlaylistGames games={sortedGames} renderAction={game => <StoreButton game={game} token={token} />} />
        </section>
      )}
    </section>
  )
}
