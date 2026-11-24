import { api } from './api'
import { trustedStoreLink } from './storeLinks'

export default function StoreButton({ game, token }) {
  const link = trustedStoreLink(game.storeUrl)
  const noteId = `store-note-${game.id}`

  if (!link) {
    return (
      <span className="store-action">
        <button type="button" className="store-button" disabled aria-describedby={noteId}>Pirkti</button>
        <span id={noteId} className="field-note">Oficiali parduotuvės nuoroda šiam žaidimui nežinoma.</span>
      </span>
    )
  }

  function recordClick() {
    api('/api/store-clicks', { method: 'POST', token, keepalive: true, body: { storeUrl: link.url } }).catch(() => {})
  }

  return (
    <span className="store-action">
      <a className="store-button" href={link.url} target="_blank" rel="noopener noreferrer"
        onClick={recordClick} onAuxClick={event => { if (event.button === 1) recordClick() }}>
        Atidaryti {link.store}
        <span className="visually-hidden"> (atsidaro naujame skirtuke)</span>
      </a>
    </span>
  )
}
