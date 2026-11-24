import { useEffect, useState } from 'react'
import { api, errorMessage } from './api'

const dateFormat = new Intl.DateTimeFormat('lt-LT', { dateStyle: 'medium', timeStyle: 'short' })
const historyPath = '/api/profile/query-history'

export default function QueryHistory({ token, onUnauthorized, onReuse }) {
  const [entries, setEntries] = useState(null)
  const [error, setError] = useState('')
  const [deleting, setDeleting] = useState(null)

  useEffect(() => {
    const request = new AbortController()
    api(historyPath, { token, signal: request.signal })
      .then(data => {
        if (!request.signal.aborted) setEntries(data)
      })
      .catch(error => {
        if (request.signal.aborted) return
        if (error.status === 401) onUnauthorized()
        else setError(errorMessage(error))
      })
    return () => request.abort()
  }, [token, onUnauthorized])

  async function remove(id) {
    setDeleting(id)
    setError('')
    try {
      await api(`${historyPath}/${id}`, { method: 'DELETE', token })
      setEntries(current => current.filter(entry => entry.id !== id))
    } catch (error) {
      if (error.status === 401) onUnauthorized()
      else if (error.status === 404) setEntries(current => current.filter(entry => entry.id !== id))
      else setError(errorMessage(error))
    } finally {
      setDeleting(null)
    }
  }

  return (
    <section className="query-history">
      <h2>Užklausų istorija</h2>
      {error && <p className="error" role="alert">{error}</p>}
      {!entries && !error && <p role="status">Įkeliama užklausų istorija…</p>}
      {entries?.length === 0 && <p>Kol kas užklausų nėra.</p>}
      {entries?.length > 0 && (
        <ul>
          {entries.map(entry => (
            <li key={entry.id}>
              <div className="history-entry">
                <button type="button" className="history-query" onClick={() => onReuse(entry.text)}>
                  {entry.text}
                </button>
                <time className="field-note" dateTime={entry.createdAt}>{dateFormat.format(new Date(entry.createdAt))}</time>
              </div>
              <button type="button" className="secondary" aria-label={`Ištrinti užklausą „${entry.text}“`}
                disabled={deleting !== null} onClick={() => remove(entry.id)}>
                {deleting === entry.id ? 'Trinama…' : 'Ištrinti'}
              </button>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
