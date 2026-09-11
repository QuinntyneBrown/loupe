// Local-only terminal-display harness for recording the loupe-api demo video.
// Runs a FIXED sequence of real curl calls against the already-running demo
// stack (see docs/demo/README.md) and streams their real stdout to a plain-text
// page. Command selection lives here, not in any request the page accepts;
// the server binds to 127.0.0.1 only and exposes no general execution endpoint.
import { createServer } from 'node:http';
import { execFile, spawn } from 'node:child_process';
import { mkdirSync, readFileSync, writeFileSync, existsSync } from 'node:fs';
import { randomUUID } from 'node:crypto';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const workerLogLines = [];
const workerLogTail = spawn('docker', ['logs', '-f', '--tail', '0', 'loupe-demo-worker'], { windowsHide: true });
let workerLogBuffer = '';
const pushWorkerLine = (chunk) => {
  workerLogBuffer += chunk.toString('utf8');
  const parts = workerLogBuffer.split(/\r?\n/);
  workerLogBuffer = parts.pop() ?? '';
  for (const raw of parts) {
    if (!raw.trim()) continue;
    // The worker's own code only logs at LogError on failure; its routine activity
    // is otherwise visible as structured EF Core command logs. Rather than hide
    // that noise entirely, compress each entry to which subsystem ran a query and
    // how long it took — real evidence of continuous background work, not prose.
    let summary = raw;
    try {
      const parsed = JSON.parse(raw);
      const entryPoint = (parsed.Scopes ?? []).map((s) => s.EntryPoint).filter(Boolean).pop();
      const elapsed = parsed.State?.elapsed;
      if (entryPoint) summary = `${entryPoint}${elapsed ? ` · query ${elapsed}ms` : ''}`;
      else summary = `${parsed.LogLevel ?? 'Info'}  ${(parsed.Message ?? raw).split('\n')[0]}`;
    } catch { /* keep raw line */ }
    workerLogLines.push(summary);
    if (workerLogLines.length > 500) workerLogLines.shift();
  }
};
workerLogTail.stdout.on('data', pushWorkerLine);
workerLogTail.stderr.on('data', pushWorkerLine);
workerLogTail.on('error', (err) => { workerLogLines.push(`(worker log tail failed to start: ${err.message})`); });
let workerLogCursor = 0;

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const runDir = path.join(__dirname, 'test-results', 'loupe-api-run');
mkdirSync(runDir, { recursive: true });
const cookieJar = path.join(runDir, 'cookies.txt');
writeFileSync(cookieJar, '');

const API = 'https://localhost:5001';
const ORIGIN = 'https://localhost:4200';

function run(cmd, args, input) {
  return new Promise((resolve) => {
    const child = execFile(cmd, args, { windowsHide: true, maxBuffer: 10 * 1024 * 1024 }, (err, stdout, stderr) => {
      resolve({ code: err ? (err.code ?? 1) : 0, stdout: stdout ?? '', stderr: stderr ?? '' });
    });
    child.stdin.end(input);
  });
}

function curl(args, input) {
  return run('curl', ['-sk', ...args], input);
}

async function csrfToken() {
  const { stdout } = await curl(['-b', cookieJar, '-c', cookieJar, '-D', '-', '-o', 'NUL', `${API}/api/session`]);
  const m = stdout.match(/^x-csrf-token:\s*(.+)$/im);
  return m ? m[1].trim() : '';
}

function extractHeader(headerText, name) {
  const m = headerText.match(new RegExp(`^${name}:\\s*(.+)$`, 'im'));
  return m ? m[1].trim() : '';
}

function truncate(value, max = 44) {
  return value.length > max ? `${value.slice(0, max)}…` : value;
}

const steps = [
  {
    name: 'sign-in',
    caption: 'Sign in as the demo photographer — Loupe validates the password and issues its own JWT',
    async run() {
      const lines = ['$ curl https://localhost:5001/api/session/csrf'];
      const protection = await curl(['-c', cookieJar, '-D', '-', '-o', 'NUL', `${API}/api/session/csrf`]);
      const csrf = extractHeader(protection.stdout, 'x-csrf-token');
      lines.push('$ curl -X POST https://localhost:5001/api/session/sign-in --data-binary @-');
      const login = await curl(['-b', cookieJar, '-c', cookieJar, '--fail-with-body',
        '-H', `X-CSRF-Token: ${csrf}`, '-H', `Origin: ${ORIGIN}`, '-H', 'Content-Type: application/json',
        '--data-binary', '@-', `${API}/api/session/sign-in`],
        JSON.stringify({ email: 'api-demo@example.com', password: 'local acceptance password' }));
      if (login.code !== 0) return { ok: false, output: 'Local sign-in failed. Check demo account provisioning.' };
      lines.push('200 — local session created; JWT stays in the HttpOnly cookie');
      lines.push('$ curl https://localhost:5001/api/session');
      const session = await curl(['-b', cookieJar, `${API}/api/session`]);
      lines.push(session.stdout.trim());
      return { ok: session.stdout.includes('"subject"'), output: lines.join('\n') };
    },
  },
  {
    name: 'upload',
    caption: 'Upload a photograph with a critique brief — real multipart upload, real libvips decode',
    async run() {
      const csrf = await csrfToken();
      const idempotencyKey = randomUUID();
      const lines = [];
      lines.push('$ curl -F Image=@sunrise.jpg -F Title="Harbor at dawn" ... https://localhost:5001/api/photographs');
      const { stdout, code } = await curl(['-b', cookieJar, '-c', cookieJar,
        '-H', `X-CSRF-Token: ${csrf}`, '-H', `Idempotency-Key: ${idempotencyKey}`, '-H', `Origin: ${ORIGIN}`,
        '-F', `Image=@${path.join(__dirname, 'fixtures', 'sunrise.jpg')};type=image/jpeg`,
        '-F', 'Title=Harbor at dawn',
        '-F', 'Intent=Show the fog lifting off the water as the sun clears the breakwater',
        '-F', 'Genre=landscape', '-F', 'Experience=Intermediate',
        `${API}/api/photographs`]);
      let id = '';
      try { id = JSON.parse(stdout).id; } catch { /* left blank on failure */ }
      lines.push(stdout.trim());
      writeFileSync(path.join(runDir, 'photograph-id.txt'), id ?? '');
      return { ok: code === 0 && !!id, output: lines.join('\n') };
    },
  },
  {
    name: 'critique',
    caption: 'Request an AI critique — a real Loupe.Worker claims the lease and completes it',
    async run() {
      const id = readFileSync(path.join(runDir, 'photograph-id.txt'), 'utf8').trim();
      const csrf = await csrfToken();
      const lines = [];
      lines.push(`$ curl -X POST https://localhost:5001/api/photographs/${id}/critique`);
      const request = await curl(['-b', cookieJar, '-c', cookieJar,
        '-H', `X-CSRF-Token: ${csrf}`, '-H', `Origin: ${ORIGIN}`, '-H', 'Content-Type: application/json',
        '-H', `Idempotency-Key: ${randomUUID()}`,
        '-d', '{"revision":1,"regenerate":false}', `${API}/api/photographs/${id}/critique`]);
      lines.push(request.stdout.trim());
      let requestStatus = '';
      try { requestStatus = JSON.parse(request.stdout).status; } catch { /* fall through to failure below */ }
      if (requestStatus !== 'Queued' && requestStatus !== 'Running' && requestStatus !== 'Succeeded') {
        return { ok: false, output: lines.join('\n') };
      }

      lines.push('');
      lines.push(`$ curl https://localhost:5001/api/photographs/${id}/critique/operation  (polling)`);
      let status = '';
      for (let attempt = 0; attempt < 20; attempt++) {
        const poll = await curl(['-b', cookieJar, `${API}/api/photographs/${id}/critique/operation`]);
        try { status = JSON.parse(poll.stdout).status; } catch { status = ''; }
        lines.push(`  status: ${status}`);
        if (status === 'Succeeded' || status === 'Failed') break;
        await new Promise((resolve) => setTimeout(resolve, 500));
      }
      if (status !== 'Succeeded') return { ok: false, output: lines.join('\n') };

      lines.push('');
      lines.push(`$ curl https://localhost:5001/api/photographs/${id}/critique`);
      const result = await curl(['-b', cookieJar, `${API}/api/photographs/${id}/critique`]);
      let summary = result.stdout;
      try {
        const parsed = JSON.parse(result.stdout);
        summary = JSON.stringify({ mode: parsed.mode, model: parsed.model,
          strengths: parsed.content.strengths.length, improvements: parsed.content.improvements.length,
          firstImprovement: parsed.content.improvements[0]?.observation }, null, 2);
      } catch { /* fall back to raw output */ }
      lines.push(summary);
      return { ok: true, output: lines.join('\n') };
    },
  },
  {
    name: 'reference-import',
    caption: 'Save a reference by URL — real SSRF-checked outbound fetch and de-duplication',
    async run() {
      const csrf = await csrfToken();
      const sourceUrl = 'https://upload.wikimedia.org/wikipedia/commons/4/47/PNG_transparency_demonstration_1.png';
      const lines = [];
      lines.push(`$ curl -X POST https://localhost:5001/api/references/links -d '{"sourceUrl":"${sourceUrl}"}'`);
      const save = await curl(['-b', cookieJar, '-c', cookieJar,
        '-H', `X-CSRF-Token: ${csrf}`, '-H', `Origin: ${ORIGIN}`, '-H', 'Content-Type: application/json',
        '-H', `Idempotency-Key: ${randomUUID()}`,
        '-d', JSON.stringify({ sourceUrl }), `${API}/api/references/links`]);
      let id = '';
      try { id = JSON.parse(save.stdout).reference.id; } catch { /* left blank on failure */ }
      lines.push(save.stdout.trim());
      if (!id) return { ok: false, output: lines.join('\n') };

      lines.push('');
      lines.push(`$ curl -X POST https://localhost:5001/api/references/${id}/imports`);
      const csrf2 = await csrfToken();
      const importReq = await curl(['-b', cookieJar, '-c', cookieJar,
        '-H', `X-CSRF-Token: ${csrf2}`, '-H', `Origin: ${ORIGIN}`, '-H', 'Content-Type: application/json',
        '-H', `Idempotency-Key: ${randomUUID()}`,
        '-d', '{"revision":1}', `${API}/api/references/${id}/imports`]);
      lines.push(importReq.stdout.trim());
      let importStatus = '';
      try { importStatus = JSON.parse(importReq.stdout).status; } catch { /* falls through to failure */ }
      lines.push('');
      lines.push('(admission accepted with a real SSRF-checked, robots-respecting fetcher — see docs/demo/README.md');
      lines.push(' for why this recording does not show it reach Completed: import execution has no worker');
      lines.push(' capability implemented yet in this codebase.)');
      return { ok: !!id && importStatus === 'Queued', output: lines.join('\n') };
    },
  },
  {
    name: 'delete',
    caption: 'Delete the photograph — a real Loupe.Worker performs durable cleanup',
    async run() {
      const id = readFileSync(path.join(runDir, 'photograph-id.txt'), 'utf8').trim();
      const current = await curl(['-b', cookieJar, `${API}/api/photographs/${id}`]);
      const revision = JSON.parse(current.stdout).revision;
      const csrf = await csrfToken();
      const lines = [];
      lines.push(`$ curl -X DELETE https://localhost:5001/api/photographs/${id}?revision=${revision}`);
      const del = await curl(['-b', cookieJar, '-c', cookieJar,
        '-H', `X-CSRF-Token: ${csrf}`, '-H', `Origin: ${ORIGIN}`, '-X', 'DELETE',
        `${API}/api/photographs/${id}?revision=${revision}`]);
      let deletionId = '';
      try { deletionId = JSON.parse(del.stdout).id; } catch { /* left blank on failure */ }
      lines.push(del.stdout.trim());

      lines.push('');
      lines.push(`$ curl https://localhost:5001/api/deletions/${deletionId}  (polling)`);
      let status = '';
      for (let attempt = 0; attempt < 40; attempt++) {
        const poll = await curl(['-b', cookieJar, `${API}/api/deletions/${deletionId}`]);
        try { status = JSON.parse(poll.stdout).status; } catch { status = ''; }
        lines.push(`  status: ${status}`);
        if (status === 'Completed') break;
        await new Promise((resolve) => setTimeout(resolve, 500));
      }
      return { ok: status === 'Completed', output: lines.join('\n') };
    },
  },
];

const server = createServer(async (req, res) => {
  if (req.method === 'GET' && req.url === '/') {
    res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8' });
    res.end(readFileSync(path.join(__dirname, 'loupe-api-terminal.html')));
    return;
  }
  if (req.method === 'GET' && req.url === '/steps') {
    res.writeHead(200, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify(steps.map((s) => ({ name: s.name, caption: s.caption }))));
    return;
  }
  if (req.method === 'GET' && req.url === '/worker-log') {
    const fresh = workerLogLines.slice(workerLogCursor);
    workerLogCursor = workerLogLines.length;
    res.writeHead(200, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify(fresh));
    return;
  }
  const match = req.url?.match(/^\/steps\/([a-z-]+)$/);
  if (req.method === 'POST' && match) {
    const step = steps.find((s) => s.name === match[1]);
    if (!step) { res.writeHead(404); res.end(); return; }
    try {
      const result = await step.run();
      res.writeHead(200, { 'Content-Type': 'application/json' });
      res.end(JSON.stringify(result));
    } catch (err) {
      res.writeHead(200, { 'Content-Type': 'application/json' });
      res.end(JSON.stringify({ ok: false, output: String(err) }));
    }
    return;
  }
  res.writeHead(404);
  res.end();
});

const port = Number(process.env.PORT ?? 4302);
server.listen(port, '127.0.0.1', () => {
  console.log(`loupe-api terminal harness listening on http://127.0.0.1:${port}`);
});

const shutdown = () => { workerLogTail.kill(); server.close(() => process.exit(0)); };
process.on('SIGINT', shutdown);
process.on('SIGTERM', shutdown);
