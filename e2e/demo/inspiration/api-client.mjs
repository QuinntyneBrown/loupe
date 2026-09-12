// Minimal authenticated client for the real Loupe.Api, used to seed the demo
// library and to verify persisted state after the recording. It performs the
// same protocol as the production Angular adapter: anonymous CSRF proof,
// credential sign-in, then identity-bound antiforgery tokens on every mutation.
// Trust the harness dev certificate with NODE_EXTRA_CA_CERTS before running.
export class ApiClient {
  constructor(apiUrl, origin) { this.apiUrl = apiUrl.replace(/\/$/, ''); this.origin = origin; this.cookies = new Map(); this.csrf = null; }
  async signIn(email, password) {
    const protection = await this.request('GET', '/api/session/csrf');
    await this.request('POST', '/api/session/sign-in', { email, password }, { 'X-CSRF-Token': protection.headers.get('X-CSRF-Token') });
    const session = await this.request('GET', '/api/session');
    this.csrf = session.headers.get('X-CSRF-Token');
    return session.body;
  }
  async request(method, path, body, headers = {}) {
    const init = { method, headers: { Origin: this.origin, Cookie: [...this.cookies].map(([name, value]) => `${name}=${value}`).join('; '), ...headers } };
    if (body instanceof FormData) init.body = body;
    else if (body !== undefined) { init.body = JSON.stringify(body); init.headers['Content-Type'] = 'application/json'; }
    if (method !== 'GET' && this.csrf && !init.headers['X-CSRF-Token']) init.headers['X-CSRF-Token'] = this.csrf;
    const response = await fetch(this.apiUrl + path, init);
    for (const cookie of response.headers.getSetCookie()) {
      const [pair] = cookie.split(';'); const index = pair.indexOf('='); this.cookies.set(pair.slice(0, index).trim(), pair.slice(index + 1).trim());
    }
    const text = await response.text();
    if (!response.ok) throw new Error(`${method} ${path} -> ${response.status}: ${text.slice(0, 300)}`);
    return { status: response.status, headers: response.headers, body: text ? JSON.parse(text) : null };
  }
  async get(path) { return (await this.request('GET', path)).body; }
  async post(path, body, headers) { return (await this.request('POST', path, body, headers)).body; }
  async put(path, body) { return (await this.request('PUT', path, body)).body; }
}
