const COMMAND_API = "http://localhost:3001";
const QUERY_API = "http://localhost:3002";

async function request(base, path, options = {}) {
  const { silent = false, ...fetchOptions } = options;
  const started = performance.now();
  const headers = { ...(fetchOptions.headers || {}) };
  if (fetchOptions.body && !headers["Content-Type"]) {
    headers["Content-Type"] = "application/json";
  }

  const response = await fetch(`${base}${path}`, {
    ...fetchOptions,
    headers
  });

  const elapsed = Math.round(performance.now() - started);
  let body = null;
  const text = await response.text();
  if (text) {
    try {
      body = JSON.parse(text);
    } catch {
      body = { raw: text };
    }
  }

  const entry = {
    method: (fetchOptions.method || "GET").toUpperCase(),
    url: `${base}${path}`,
    status: response.status,
    ok: response.ok,
    elapsed,
    silent,
    side: base === COMMAND_API ? "command" : "query"
  };

  if (!response.ok) {
    const message = body?.error || body?.message || `Error ${response.status} en ${path}`;
    throw Object.assign(new Error(message), { entry });
  }

  return { data: body, entry };
}

export const api = {
  healthA: () => request(COMMAND_API, "/health", { silent: true }),
  healthB: () => request(QUERY_API, "/health", { silent: true }),
  listBattles: () => request(QUERY_API, "/battles", { silent: true }),
  getBattle: (id) => request(QUERY_API, `/battles/${encodeURIComponent(id)}`),
  getHistory: (id) => request(QUERY_API, `/battles/${encodeURIComponent(id)}/history`, { silent: true }),
  getStats: (id) => request(QUERY_API, `/battles/${encodeURIComponent(id)}/stats`, { silent: true }),
  startBattle: (payload) => request(COMMAND_API, "/battles/start", {
    method: "POST",
    body: JSON.stringify(payload)
  }),
  attack: (id, payload) => request(COMMAND_API, `/battles/${encodeURIComponent(id)}/attack`, {
    method: "POST",
    body: JSON.stringify(payload)
  }),
  heal: (id, payload) => request(COMMAND_API, `/battles/${encodeURIComponent(id)}/heal`, {
    method: "POST",
    body: JSON.stringify(payload)
  })
};

export function describeCall(entry, fallback) {
  if (!entry) return fallback;
  return `${entry.method} ${entry.url} → ${entry.status} (${entry.elapsed} ms)`;
}
