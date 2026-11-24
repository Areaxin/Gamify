import { useCallback, useEffect, useState } from 'react'
import { api, errorMessage } from './api'

function preferencesText(member) {
  const ageRating = member.ageRating
    ? [member.ageRating.pegi && `PEGI ${member.ageRating.pegi}`, `ESRB ${member.ageRating.esrb}`]
      .filter(Boolean).join(' / ')
    : 'nenustatytas'
  const experience = {
    Beginner: 'Pradedantysis',
    Intermediate: 'Vidutinis',
    Experienced: 'Patyręs',
  }[member.preferredExperience] ?? 'nenurodyta'

  return [
    `Platformos: ${member.platforms.length ? member.platforms.join(', ') : 'nepasirinktos'}`,
    `PC lygis: ${member.platforms.includes('PC') ? member.pcLevel ?? 'nenurodytas' : 'neturi PC'}`,
    `Gimimo data / reitingas: ${member.birthDate ?? 'nenurodyta'}${member.birthDate ? ` / ${ageRating}` : ''}`,
    `Biudžetas: ${member.preferredBudgetEur == null ? 'nenurodytas' : `${member.preferredBudgetEur} €`}`,
    `Patirtis: ${experience}`,
  ]
}

export default function GroupManagement({ token, onUnauthorized }) {
  const [groups, setGroups] = useState([])
  const [groupName, setGroupName] = useState('')
  const [search, setSearch] = useState('')
  const [users, setUsers] = useState([])
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')

  const loadGroups = useCallback(async signal => {
    const data = await api('/api/groups', { token, signal })
    if (!signal?.aborted) setGroups(data)
    return data
  }, [token])

  useEffect(() => {
    const request = new AbortController()
    api('/api/groups', { token, signal: request.signal })
      .then(data => {
        if (!request.signal.aborted) setGroups(data)
      })
      .catch(requestError => {
        if (request.signal.aborted) return
        if (requestError.status === 401) onUnauthorized()
        else setError(errorMessage(requestError))
      })
      .finally(() => {
        if (!request.signal.aborted) setLoading(false)
      })
    return () => request.abort()
  }, [loadGroups, onUnauthorized, token])

  async function createGroup(event) {
    event.preventDefault()
    setError('')
    setMessage('')
    setBusy(true)
    try {
      const group = await api('/api/groups', {
        method: 'POST',
        token,
        body: { name: groupName },
      })
      setGroups(current => [...current, group])
      setGroupName('')
      setMessage(`Grupė „${group.name}“ sukurta.`)
    } catch (requestError) {
      if (requestError.status === 401) onUnauthorized()
      else setError(errorMessage(requestError))
    } finally {
      setBusy(false)
    }
  }

  async function searchUsers(event) {
    event.preventDefault()
    setError('')
    setMessage('')
    if (search.trim().length < 2) {
      setError('Paieškai įveskite bent 2 simbolius.')
      return
    }
    setBusy(true)
    try {
      setUsers(await api(`/api/groups/users?query=${encodeURIComponent(search.trim())}`, { token }))
    } catch (requestError) {
      if (requestError.status === 401) onUnauthorized()
      else setError(errorMessage(requestError))
    } finally {
      setBusy(false)
    }
  }

  async function addMember(group, member) {
    setError('')
    setMessage('')
    setBusy(true)
    try {
      await api(`/api/groups/${group.id}/members`, {
        method: 'POST',
        token,
        body: { userId: member.id },
      })
      await loadGroups()
      setMessage(`${member.name} pridėtas į grupę „${group.name}“.`)
    } catch (requestError) {
      if (requestError.status === 401) onUnauthorized()
      else setError(errorMessage(requestError))
    } finally {
      setBusy(false)
    }
  }

  async function removeMember(group, member) {
    setError('')
    setMessage('')
    setBusy(true)
    try {
      await api(`/api/groups/${group.id}/members/${member.id}`, { method: 'DELETE', token })
      await loadGroups()
      setMessage(`${member.name} pašalintas iš grupės „${group.name}“.`)
    } catch (requestError) {
      if (requestError.status === 401) onUnauthorized()
      else setError(errorMessage(requestError))
    } finally {
      setBusy(false)
    }
  }

  async function renameGroup(group, name) {
    const trimmed = name.trim()
    if (!trimmed || trimmed === group.name) return
    setError('')
    setBusy(true)
    try {
      await api(`/api/groups/${group.id}`, {
        method: 'PUT',
        token,
        body: { name: trimmed },
      })
      setGroups(current => current.map(item => item.id === group.id ? { ...item, name: trimmed } : item))
      setMessage('Grupės pavadinimas atnaujintas.')
    } catch (requestError) {
      if (requestError.status === 401) onUnauthorized()
      else setError(errorMessage(requestError))
    } finally {
      setBusy(false)
    }
  }

  async function deleteGroup(group) {
    if (!window.confirm(`Ar tikrai norite ištrinti grupę „${group.name}“? Šio veiksmo atšaukti negalima.`)) return
    setError('')
    setMessage('')
    setBusy(true)
    try {
      await api(`/api/groups/${group.id}`, { method: 'DELETE', token })
      setGroups(current => current.filter(item => item.id !== group.id))
      setMessage(`Grupė „${group.name}“ ištrinta.`)
    } catch (requestError) {
      if (requestError.status === 401) onUnauthorized()
      else setError(errorMessage(requestError))
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className="group-management">
      <h2>Mano grupės</h2>
      <p className="field-note">
        Sukurkite grupę ir pridėkite registruotus naudotojus. Jų platformos, PC lygis, amžiaus reitingas
        ir išsaugoti rekomendacijų pageidavimai bus naudojami grupės paieškoje.
      </p>
      {error && <p className="error" role="alert">{error}</p>}
      {message && <p className="playlist-message" role="status">{message}</p>}

      <form className="group-create-form" onSubmit={createGroup}>
        <label>
          Naujos grupės pavadinimas
          <input required maxLength={80} value={groupName}
            onChange={event => setGroupName(event.target.value)} placeholder="Pvz., Savaitgalio draugai" />
        </label>
        <button type="submit" disabled={busy}>Sukurti grupę</button>
      </form>

      <form className="group-user-search" onSubmit={searchUsers}>
        <label>
          Rasti registruotą naudotoją pagal vardą arba el. paštą
          <input value={search} maxLength={80} onChange={event => {
            setSearch(event.target.value)
            setUsers([])
          }}
            placeholder="Įveskite vardą arba el. pašto dalį" />
        </label>
        <button type="submit" className="secondary" disabled={busy}>Ieškoti naudotojų</button>
      </form>
      {users.length > 0 && (
        <div className="group-user-results" aria-label="Rasti naudotojai">
          {users.map(member => (
            <article key={member.id} className="group-user-result">
              <div>
                <strong>{member.name}</strong>
                <span>{member.email}</span>
              </div>
              {groups.map(group => {
                const isMember = group.members.some(item => item.id === member.id)
                return (
                  <button key={group.id} type="button" className="secondary"
                    disabled={busy || isMember || group.members.length >= 5}
                    onClick={() => addMember(group, member)}>
                    {isMember ? `Jau grupėje „${group.name}“` : `Pridėti į „${group.name}“`}
                  </button>
                )
              })}
            </article>
          ))}
          {users.length === 10 && <p className="field-note">Rodoma iki 10 rezultatų. Patikslinkite paiešką.</p>}
        </div>
      )}
      {!loading && users.length === 0 && search.trim().length >= 2 && (
        <p className="field-note">Pagal šią paiešką naudotojų nerasta arba jie jau neprieinami.</p>
      )}

      {loading && <p role="status">Įkeliamos grupės…</p>}
      {!loading && groups.length === 0 && (
        <p className="field-note">Grupių dar nėra. Sukurkite grupę — jūs būsite pirmasis jos narys.</p>
      )}
      <div className="group-card-list">
        {groups.map(group => (
          <article className="group-card" key={group.id}>
            <header>
              <label>
                Grupės pavadinimas
                <input key={group.name} defaultValue={group.name} maxLength={80}
                  onBlur={event => renameGroup(group, event.target.value)}
                  onKeyDown={event => {
                    if (event.key === 'Enter') {
                      event.preventDefault()
                      event.currentTarget.blur()
                    }
                  }} />
              </label>
              <button type="button" className="secondary" disabled={busy}
                onClick={() => deleteGroup(group)}>Ištrinti grupę</button>
            </header>
            <p className="field-note">{group.members.length} / 5 nariai (įskaitant grupės kūrėją)</p>
            <div className="group-card-members">
              {group.members.map(member => (
                <section key={member.id} className="group-member-card">
                  <div className="group-member-heading">
                    <strong>{member.name}{member.id === group.ownerUserId ? ' (kūrėjas)' : ''}</strong>
                    <span>{member.email}</span>
                  </div>
                  <ul>
                    {preferencesText(member).map(preference => <li key={preference}>{preference}</li>)}
                  </ul>
                  {member.id !== group.ownerUserId && (
                    <button type="button" className="secondary" disabled={busy}
                      onClick={() => removeMember(group, member)}>Pašalinti iš grupės</button>
                  )}
                </section>
              ))}
            </div>
          </article>
        ))}
      </div>
    </section>
  )
}
