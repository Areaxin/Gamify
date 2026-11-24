import { api } from './api'
import { trustedStoreLink } from './storeLinks'

export default function StoreButton({ game, token }) {
  const link = trustedStoreLink(game.storeUrl)
  const noteId = `store-note-${game.id}`

  if (!link) {
    return (
      <span className="store-action">
        <button type="button" className="store-button" disabled aria-describedby={noteId}>Pirkti</button>
        <span id={noteId} className="field-note">Oficiali Steam ar Epic parduotuvės nuoroda šiam žaidimui nežinoma.</span>
      </span>
    )
  }

  function recordClick() {
    api(`/api/games/${game.id}/store-clicks`, { method: 'POST', token, keepalive: true }).catch(() => {})
  }

  return (
    <span className="store-action">
      <a className="store-button" href={link.url} target="_blank" rel="noopener noreferrer"
        onClick={recordClick} onAuxClick={event => { if (event.button === 1) recordClick() }}>
        Pirkti {link.store} parduotuvėje
        <span className="visually-hidden"> (atsidaro naujame skirtuke)</span>
      </a>
    </span>
  )
}
