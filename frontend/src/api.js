const apiBaseUrl = import.meta.env.VITE_API_BASE_URL?.trim() || 'http://localhost:5218'

export async function api(path, { token, body, method = 'GET', signal, keepalive } = {}) {
  const headers = {}
  if (token) headers.Authorization = `Bearer ${token}`
  if (body) headers['Content-Type'] = 'application/json'

  const response = await fetch(new URL(path, apiBaseUrl), {
    method,
    headers,
    body: body ? JSON.stringify(body) : undefined,
    signal,
    keepalive,
  })
  const data = response.headers.get('content-type')?.includes('json')
    ? await response.json()
    : null

  if (!response.ok) {
    const message = response.status === 429 ? 'Per daug bandymų. Pabandykite po minutės.' : 'Užklausos įvykdyti nepavyko.'
    throw Object.assign(new Error(data?.message || message), { status: response.status })
  }

  return data
}

export function errorMessage(error) {
  return error.status ? error.message : 'Nepavyko susisiekti su serveriu.'
}
