const fs = require('fs');
const path = require('path');

function copyDir(src, dest) {
  if (!fs.existsSync(dest)) fs.mkdirSync(dest, { recursive: true });
  for (const entry of fs.readdirSync(src)) {
    const s = path.join(src, entry);
    const d = path.join(dest, entry);
    const stat = fs.statSync(s);
    if (stat.isDirectory()) copyDir(s, d);
    else fs.copyFileSync(s, d);
  }
}

const outDirs = [
  'dist/win-x64/public',
  'dist/linux-x64/public',
  'dist/macos-x64/public',
];

for (const dir of outDirs) {
  try { copyDir(path.join(__dirname, '..', 'public'), path.join(__dirname, '..', dir)); } catch (_) {}
}

console.log('Public assets copied.');


