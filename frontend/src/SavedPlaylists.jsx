import { useEffect, useState } from 'react'
import { api, errorMessage } from './api'
import PlaylistGames from './PlaylistGames'
import StoreButton from './StoreButton'

export default function SavedPlaylists({ token, onUnauthorized }) {
  const [playlists, setPlaylists] = useState([])
  const [opened, setOpened] = useState(null)
  const [editing, setEditing] = useState(null)
  const [busy, setBusy] = useState(false)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [removingGameId, setRemovingGameId] = useState(null)

  useEffect(() => {
    const request = new AbortController()
    api('/api/saved-playlists', { token, signal: request.signal })
      .then(data => {
        if (!request.signal.aborted) setPlaylists(data)
      })
      .catch(error => {
        if (request.signal.aborted) return
        if (error.status === 401) onUnauthorized()
        else setError(errorMessage(error))
      })
      .finally(() => {
        if (!request.signal.aborted) setLoading(false)
      })
    return () => request.abort()
  }, [token, onUnauthorized])

  async function openPlaylist(id) {
    setError('')
    if (opened?.id === id) {
      setOpened(null)
      return
    }

    setBusy(true)
    try {
      setOpened(await api(`/api/saved-playlists/${id}`, { token }))
    } catch (error) {
      if (error.status === 401) onUnauthorized()
      else setError(errorMessage(error))
    } finally {
      setBusy(false)
    }
  }

  async function renamePlaylist(event, id) {
    event.preventDefault()
    setError('')
    setBusy(true)
    try {
      const renamed = await api(`/api/saved-playlists/${id}`, {
        method: 'PUT',
        token,
        body: { name: editing.name },
      })
      setPlaylists(current => current.map(playlist => playlist.id === id ? renamed : playlist))
      setOpened(current => current?.id === id ? { ...current, name: renamed.name } : current)
      setEditing(null)
    } catch (error) {
      if (error.status === 401) onUnauthorized()
      else setError(errorMessage(error))
    } finally {
      setBusy(false)
    }
  }

  async function deletePlaylist(id) {
    setError('')
    setBusy(true)
    try {
      await api(`/api/saved-playlists/${id}`, { method: 'DELETE', token })
      setPlaylists(current => current.filter(playlist => playlist.id !== id))
      setOpened(current => current?.id === id ? null : current)
      setEditing(current => current?.id === id ? null : current)
    } catch (error) {
      if (error.status === 401) onUnauthorized()
      else setError(errorMessage(error))
    } finally {
      setBusy(false)
    }
  }

  async function removeGame(playlistId, game) {
    if (!window.confirm(`Pašalinti „${game.title}“ iš išsaugoto grojaraščio?`))
      return

    setError('')
    setRemovingGameId(game.id)
    try {
      await api(`/api/saved-playlists/${playlistId}/games/${game.id}`, {
        method: 'DELETE',
        token,
      })
      setOpened(current => current?.id === playlistId
        ? { ...current, games: current.games.filter(item => item.id !== game.id) }
        : current)
    } catch (error) {
      if (error.status === 401) onUnauthorized()
      else setError(errorMessage(error))
    } finally {
      setRemovingGameId(null)
    }
  }

  return (
    <section className="saved-playlists">
      <h2>Išsaugoti grojaraščiai</h2>
      {error && <p className="error" role="alert">{error}</p>}
      {loading && <p role="status">Įkeliami grojaraščiai…</p>}
      {!loading && playlists.length === 0 && <p className="field-note">Išsaugotų grojaraščių dar nėra.</p>}
      {playlists.length > 0 && (
        <ul className="saved-playlist-list">
          {playlists.map(playlist => (
            <li key={playlist.id}>
              {editing?.id === playlist.id ? (
                <form onSubmit={event => renamePlaylist(event, playlist.id)}>
                  <label>
                    Grojaraščio pavadinimas
                    <input
                      autoFocus
                      required
                      maxLength={100}
                      value={editing.name}
                      onChange={event => setEditing({ ...editing, name: event.target.value })}
                    />
                  </label>
                  <div className="playlist-actions">
                    <button type="submit" disabled={busy}>Išsaugoti pavadinimą</button>
                    <button type="button" className="secondary" disabled={busy} onClick={() => setEditing(null)}>Atšaukti</button>
                  </div>
                </form>
              ) : (
                <>
                  <div>
                    <strong>{playlist.name}</strong>
                    <p className="field-note">
                      {new Date(playlist.createdAt).toLocaleDateString('lt-LT')}
                    </p>
                  </div>
                  <div className="playlist-actions">
                    <button type="button" disabled={busy} aria-expanded={opened?.id === playlist.id}
                      onClick={() => openPlaylist(playlist.id)}>
                      {opened?.id === playlist.id ? 'Uždaryti' : 'Atidaryti'}
                    </button>
                    <button type="button" className="secondary" disabled={busy}
                      onClick={() => setEditing({ id: playlist.id, name: playlist.name })}>
                      Pervadinti
                    </button>
                    <button type="button" className="secondary" disabled={busy}
                      onClick={() => deletePlaylist(playlist.id)}>
                      Ištrinti
                    </button>
                  </div>
                </>
              )}
              {opened?.id === playlist.id && (
                <div className="opened-playlist">
                  <h3>{opened.name}</h3>
                  {opened.games.length === 0 ? (
                    <p className="field-note">Šiame grojaraštyje žaidimų nėra.</p>
                  ) : (
                    <PlaylistGames games={opened.games}
                      renderAction={game => (
                        <>
                          <StoreButton game={game} token={token} />
                          <button
                            type="button"
                            className="secondary remove-game"
                            disabled={busy || removingGameId !== null}
                            onClick={() => removeGame(playlist.id, game)}
                          >
                            {removingGameId === game.id ? 'Šalinama…' : 'Pašalinti'}
                          </button>
                        </>
                      )}
                    />
                  )}
                </div>
              )}
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
