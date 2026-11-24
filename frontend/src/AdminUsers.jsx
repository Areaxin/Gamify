import { useEffect, useState } from 'react'
import { api, errorMessage } from './api'

const dateFormat = new Intl.DateTimeFormat('lt-LT', { dateStyle: 'medium', timeStyle: 'short' })

export default function AdminUsers({ token, currentEmail, onUnauthorized, onAccountDeleted }) {
  const [users, setUsers] = useState(null)
  const [error, setError] = useState('')
  const [deletingId, setDeletingId] = useState(null)

  useEffect(() => {
    const request = new AbortController()
    api('/api/admin/users', { token, signal: request.signal })
      .then(data => {
        if (!request.signal.aborted) setUsers(data)
      })
      .catch(error => {
        if (request.signal.aborted) return
        if (error.status === 401) onUnauthorized()
        else setError(errorMessage(error))
      })
    return () => request.abort()
  }, [token, onUnauthorized])

  async function deleteUser(user) {
    const confirmed = window.confirm(
      `Ar tikrai norite negrįžtamai ištrinti paskyrą ID ${user.id} (${user.username})? Šis veiksmas negrįžtamas.`,
    )
    if (!confirmed) return

    setError('')
    setDeletingId(user.id)
    try {
      await api(`/api/admin/users/${user.id}`, { method: 'DELETE', token })
      setUsers(current => current.filter(item => item.id !== user.id))
      if (user.email.toLowerCase() === currentEmail.toLowerCase())
        onAccountDeleted()
    } catch (error) {
      if (error.status === 401) onUnauthorized()
      else setError(errorMessage(error))
    } finally {
      setDeletingId(null)
    }
  }

  return (
    <section className="admin-users">
      <header className="page-heading">
        <h1>Vartotojų administravimas</h1>
        <p>Peržiūrėkite paskyras arba negrįžtamai jas ištrinkite.</p>
      </header>
      {error && <p className="error" role="alert">{error}</p>}
      {!users && !error && <p role="status">Įkeliami vartotojai…</p>}
      {users?.length === 0 && <p className="field-note">Registruotų vartotojų nėra.</p>}
      {users?.length > 0 && (
        <div className="admin-users-table-wrap">
          <table>
            <thead>
              <tr>
                <th scope="col">ID</th>
                <th scope="col">Vardas</th>
                <th scope="col">El. paštas</th>
                <th scope="col">Registracijos data</th>
                <th scope="col"><span className="visually-hidden">Veiksmai</span></th>
              </tr>
            </thead>
            <tbody>
              {users.map(user => (
                <tr key={user.id}>
                  <td>{user.id}</td>
                  <td>{user.username}</td>
                  <td>{user.email}</td>
                  <td><time dateTime={user.registeredAt}>{dateFormat.format(new Date(user.registeredAt))}</time></td>
                  <td>
                    <button type="button" className="danger-button" disabled={deletingId !== null}
                      aria-label={`Ištrinti paskyrą ID ${user.id}`}
                      onClick={() => deleteUser(user)}>
                      {deletingId === user.id ? 'Trinama…' : 'Ištrinti'}
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  )
}
