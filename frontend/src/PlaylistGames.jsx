const euro = new Intl.NumberFormat('lt-LT', { style: 'currency', currency: 'EUR' })

function fallbackCover(title) {
  const safeTitle = title.replace(/[&<>"']/g, character => ({
    '&': '&amp;',
    '<': '&lt;',
    '>': '&gt;',
    '"': '&quot;',
    "'": '&apos;',
  })[character])
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 400 600"><rect width="400" height="600" fill="#4049aa"/><circle cx="200" cy="210" r="95" fill="#fff" fill-opacity=".12"/><path d="M140 210h120m-60-60v120" stroke="#fff" stroke-opacity=".7" stroke-width="12"/><text x="200" y="390" fill="#fff" font-family="sans-serif" font-size="30" text-anchor="middle">${safeTitle}</text></svg>`
  return `data:image/svg+xml,${encodeURIComponent(svg)}`
}

export default function PlaylistGames({ games, renderAction }) {
  return (
    <ul className="playlist-games">
      {games.map(game => (
        <li key={game.id}>
          <img
            className="game-cover"
            src={game.coverImageUrl || fallbackCover(game.title)}
            alt={`„${game.title}“ viršelis`}
            loading="lazy"
            onError={event => {
              const image = event.currentTarget
              image.onerror = null
              image.src = fallbackCover(game.title)
            }}
          />
          <div className="game-details">
            <h3>{game.title}</h3>
            <strong className="match-score">{game.matchPercentage}% atitikimas</strong>
            <p>{game.explanation}</p>
            <span><strong>Kaina:</strong> {game.priceCents == null ? 'Kainą žiūrėkite parduotuvėje'
              : game.priceCents === 0 ? 'Nemokamas' : euro.format(game.priceCents / 100)}</span>
            <span><strong>Platformos:</strong> {game.platforms.join(', ')}</span>
            <span className="field-note">
              {game.maxPlayers === 1 ? '1 žaidėjas' : `Iki ${game.maxPlayers} žaidėjų`}
              {game.pegi != null && ` · PEGI ${game.pegi}`}
              {game.esrb && ` · ESRB ${game.esrb}`}
            </span>
            {game.ratingUrl && <a className="field-note" href={game.ratingUrl} target="_blank"
              rel="noopener noreferrer">Amžiaus reitingo šaltinis</a>}
            {renderAction?.(game)}
          </div>
        </li>
      ))}
    </ul>
  )
}
