import { Gamepad2 } from 'lucide-react'
import { useState } from 'react'

const euro = new Intl.NumberFormat('lt-LT', { style: 'currency', currency: 'EUR' })

function GameCover({ game }) {
  const [failed, setFailed] = useState(false)
  const appId = game.storeUrl?.match(/^https:\/\/store\.steampowered\.com\/app\/(\d+)\//)?.[1]
  const cover = appId ? `https://cdn.akamai.steamstatic.com/steam/apps/${appId}/header.jpg` : game.coverImageUrl

  return cover && !failed ? (
    <img className="game-cover" src={cover} alt="" loading="lazy" onError={() => setFailed(true)} />
  ) : <div className="game-cover cover-placeholder" aria-hidden="true"><Gamepad2 size={48} strokeWidth={1.4} /></div>
}

export default function PlaylistGames({ games, renderAction }) {
  return (
    <ul className="playlist-games">
      {games.map(game => (
        <li key={game.id}>
          <GameCover game={game} />
          <div className="game-details">
            <h3>{game.title}</h3>
            <strong className="match-score">{game.matchPercentage}% atitikimas</strong>
            <p>{game.explanation}</p>
            <div className="game-tags">
              {game.platforms.map(platform => <span key={platform}>{platform}</span>)}
              <span>{game.maxPlayers === 1 ? '1 žaidėjas' : `Iki ${game.maxPlayers} žaidėjų`}</span>
              {game.pegi != null && <span>PEGI {game.pegi}</span>}
              {game.esrb && <span>ESRB {game.esrb}</span>}
            </div>
            {game.ratingUrl && <a className="rating-link" href={game.ratingUrl} target="_blank"
              rel="noopener noreferrer">Amžiaus reitingo šaltinis</a>}
            <strong className={game.priceCents == null ? 'field-note game-price' : 'game-price'}>
              {game.priceCents == null ? 'Kaina parduotuvėje'
                : game.priceCents === 0 ? 'Nemokamas' : euro.format(game.priceCents / 100)}
            </strong>
            {renderAction?.(game)}
          </div>
        </li>
      ))}
    </ul>
  )
}
