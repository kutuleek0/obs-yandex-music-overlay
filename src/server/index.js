const express = require('express');
const cors = require('cors');
const { spawn } = require('child_process');
const path = require('path');
const http = require('http');
const WebSocket = require('ws');
const fs = require('fs');

const app = express();
app.use(cors());
app.use(express.json());

const server = http.createServer(app);
const wss = new WebSocket.Server({ server, path: '/ws' });

// Паблишер состояний
let lastState = null;
const allowEnv = (process.env.YM_ALLOW || 'yandex,music').toLowerCase().split(/[,;]+/).map(s => s.trim()).filter(Boolean);
function isAllowedAppId(appId) {
  if (!appId) return false;
  const lowered = String(appId).toLowerCase();
  return allowEnv.every(p => lowered.includes(p));
}

function broadcast(data) {
  const payload = JSON.stringify(data);
  wss.clients.forEach((client) => {
    if (client.readyState === WebSocket.OPEN) {
      client.send(payload);
    }
  });
}

// Статика оверлея
app.use('/', express.static(path.join(__dirname, '../../public')));

// REST для текущего состояния
app.get('/api/now', (req, res) => {
  res.json(lastState || { type: 'now_playing', state: 'Unknown' });
});

// Debug: текущий AUMID и активные фильтры
app.get('/api/debug/app', (req, res) => {
  res.json({ appId: lastState?.appId || null, allowEnv });
});

// Запуск helper-а SMTC
let helper = null;
function resolveHelper() {
  const cwd = process.cwd();
  const candidates = [
    path.join(cwd, 'smtc-helper.exe'),
    path.join(cwd, 'smtc-helper.dll'),
    path.join(cwd, 'smtc-helper', 'bin', 'Release', 'net8.0-windows10.0.19041.0', 'smtc-helper.exe'),
    path.join(cwd, 'smtc-helper', 'bin', 'Release', 'net8.0-windows10.0.19041.0', 'smtc-helper.dll'),
  ];
  for (const p of candidates) {
    if (fs.existsSync(p)) return p;
  }
  return null;
}

function startHelper() {
  const helperPath = resolveHelper();
  if (!helperPath) {
    console.error('[smtc-helper] build outputs not found, please run: dotnet build smtc-helper/smtc-helper.csproj -c Release or dotnet publish -r win-x64 -c Release');
    return;
  }
  const ext = path.extname(helperPath).toLowerCase();
  const isDll = ext === '.dll';
  const cmd = isDll ? 'dotnet' : helperPath;
  const args = isDll ? [helperPath] : [];

  helper = spawn(cmd, args, { windowsHide: true });
  console.log(`[smtc-helper] started: ${helper.pid}`);

  let buffer = '';
  helper.stdout.setEncoding('utf8');
  helper.stdout.on('data', (chunk) => {
    buffer += chunk;
    let idx;
    while ((idx = buffer.indexOf('\n')) >= 0) {
      const line = buffer.slice(0, idx).trim();
      buffer = buffer.slice(idx + 1);
      if (line.length === 0) continue;
      try {
        const obj = JSON.parse(line);
        if (!isAllowedAppId(obj.appId)) {
          // Игнор не-Яндекс сессий; сохраняем служебное состояние
          lastState = { type: 'now_playing', state: 'NotYandex' };
          broadcast({ type: 'now_playing', data: lastState });
          continue;
        }
        lastState = obj;
        broadcast({ type: 'now_playing', data: obj });
      } catch (e) {
        console.error('Failed to parse helper line', e, line);
      }
    }
  });

  helper.stderr.setEncoding('utf8');
  helper.stderr.on('data', (d) => console.error('[smtc-helper]', d.trim()));

  helper.on('exit', (code) => {
    console.error(`[smtc-helper] exited with code ${code}`);
    lastState = { type: 'now_playing', state: 'HelperExited' };
    setTimeout(startHelper, 3000);
  });
}

// Вебсокет приветствие
wss.on('connection', (ws) => {
  if (lastState) ws.send(JSON.stringify({ type: 'now_playing', data: lastState }));
});

const PORT = process.env.PORT || 3000;
server.listen(PORT, () => {
  console.log(`Server listening on http://localhost:${PORT}`);
  startHelper();
});


