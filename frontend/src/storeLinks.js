// GAM-US12: tas pats baltasis sąrašas kaip backend StoreLinks.cs. Nuoroda rodoma tik jei ji tenkina abu patikrinimus.
const allowedHosts = new Map([
  ['store.steampowered.com', 'Steam'],
  ['store.epicgames.com', 'Epic Games'],
  ['store.playstation.com', 'PlayStation Store'],
  ['www.xbox.com', 'Xbox Store'],
  ['www.microsoft.com', 'Xbox Store'],
  ['www.nintendo.com', 'Nintendo eShop'],
])

export function trustedStoreLink(url) {
  if (!url) return null
  let parsed
  try {
    parsed = new URL(url)
  } catch {
    return null
  }
  if (parsed.protocol !== 'https:' || parsed.port || parsed.username || parsed.password) return null
  const store = allowedHosts.get(parsed.hostname)
  return store ? { url: parsed.href, store } : null
}
