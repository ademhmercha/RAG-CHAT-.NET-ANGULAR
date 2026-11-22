import { spawn } from 'node:child_process';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const ng = resolve(root, 'node_modules/@angular/cli/bin/ng.js');
const port = process.env['PORT'] ?? '4200';

const child = spawn(process.execPath, [ng, 'serve', '--port', port, ...process.argv.slice(2)], {
  cwd: root,
  stdio: 'inherit',
});

for (const signal of ['SIGINT', 'SIGTERM']) {
  process.on(signal, () => child.kill(signal));
}

child.on('exit', (code) => process.exit(code ?? 0));
