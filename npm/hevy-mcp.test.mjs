import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import {
  chmodSync,
  existsSync,
  mkdirSync,
  mkdtempSync,
  readFileSync,
  rmSync,
  statSync,
  writeFileSync,
} from 'node:fs';
import { tmpdir } from 'node:os';
import { delimiter, dirname, join } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

import {
  addArguments,
  dockerArguments,
  isWindowsNodeFromWsl,
  validateApiKey,
} from './hevy-mcp.mjs';

test('builds safe read-only Docker arguments', () => {
  assert.deepEqual(dockerArguments('/user/hevy.env', false), [
    'run', '--rm', '-i', '--read-only', '--tmpfs', '/tmp:rw,noexec,nosuid,size=16m',
    '--env-file', '/user/hevy.env', '-e', 'HEVY_READ_ONLY=true',
    'ghcr.io/imhalawa/hevy-mcp:0.1.2',
  ]);
});

test('builds each supported client command without embedding the API key', () => {
  const docker = dockerArguments('/user/hevy.env', true);

  assert.deepEqual(addArguments('Codex', '/usr/bin/docker', docker).slice(0, 5), [
    'mcp', 'add', 'hevy-mcp', '--', '/usr/bin/docker',
  ]);
  assert.deepEqual(addArguments('Claude Code', '/usr/bin/docker', docker).slice(0, 9), [
    'mcp', 'add', '--transport', 'stdio', '--scope', 'user', 'hevy-mcp', '--', '/usr/bin/docker',
  ]);
});

test('detects Windows Node launched from a WSL working directory', () => {
  assert.equal(isWindowsNodeFromWsl('win32', String.raw`\\wsl.localhost\Ubuntu\home\user`), true);
  assert.equal(isWindowsNodeFromWsl('win32', String.raw`\\wsl$\Ubuntu\home\user`), true);
  assert.equal(isWindowsNodeFromWsl('win32', String.raw`C:\Users\user`), false);
  assert.equal(isWindowsNodeFromWsl('linux', '/home/user'), false);
});

test('rejects keys that cannot be represented by a Docker env file', () => {
  assert.throws(() => validateApiKey('  '));
  assert.throws(() => validateApiKey('line-one\nline-two'));
});

test('setup stores the key privately and configures detected clients', () => {
  const root = mkdtempSync(join(tmpdir(), 'hevy-mcp-setup-'));
  const bin = join(root, 'bin');
  const config = join(root, 'config');
  const log = join(root, 'commands.log');
  const extension = process.platform === 'win32' ? '.cmd' : '';
  const executable = process.platform === 'win32'
    ? '@echo off\r\necho %~f0 %*>>"%HEVY_TEST_LOG%"\r\n'
    : '#!/bin/sh\nprintf \'%s %s\\n\' "$0" "$*" >> "$HEVY_TEST_LOG"\n';
  mkdirSync(bin);

  try {
    for (const command of ['docker', 'codex', 'claude']) {
      const path = join(bin, `${command}${extension}`);
      writeFileSync(path, executable);
      if (process.platform !== 'win32') chmodSync(path, 0o700);
    }

    const secretName = ['HEVY', 'API', 'KEY'].join('_');
    const secretValue = ['fixture', 'value'].join('-');
    const cli = join(dirname(fileURLToPath(import.meta.url)), 'hevy-mcp.mjs');
    const result = spawnSync(process.execPath, [cli, 'setup'], {
      encoding: 'utf8',
      env: {
        ...process.env,
        PATH: `${bin}${delimiter}${process.env.PATH}`,
        APPDATA: config,
        XDG_CONFIG_HOME: config,
        HEVY_TEST_LOG: log,
        [secretName]: secretValue,
      },
      input: 'n\n',
    });

    assert.equal(result.status, 0, result.stderr);
    const envFile = join(config, 'hevy-mcp', 'hevy.env');
    assert.equal(readFileSync(envFile, 'utf8'), `${secretName}=${secretValue}\n`);
    if (process.platform !== 'win32') assert.equal(statSync(envFile).mode & 0o777, 0o600);

    const commands = readFileSync(log, 'utf8').replaceAll(/\.cmd/gi, '');
    assert.match(commands, /docker info/);
    assert.match(commands, /docker pull ghcr\.io\/imhalawa\/hevy-mcp:0\.1\.2/);
    assert.match(commands, /codex mcp remove hevy-mcp/);
    assert.match(commands, /codex mcp add hevy-mcp -- .*docker run/);
    assert.match(commands, /claude mcp remove --scope user hevy-mcp/);
    assert.match(commands, /claude mcp add --transport stdio --scope user hevy-mcp -- .*docker run/);
    assert.doesNotMatch(commands, /mcp remove (?:--scope user )?hevy(?:\s|$)/);

    writeFileSync(log, '');
    const uninstall = spawnSync(process.execPath, [cli, 'uninstall'], {
      encoding: 'utf8',
      env: {
        ...process.env,
        PATH: `${bin}${delimiter}${process.env.PATH}`,
        APPDATA: config,
        XDG_CONFIG_HOME: config,
        HEVY_TEST_LOG: log,
      },
    });

    assert.equal(uninstall.status, 0, uninstall.stderr);
    assert.equal(existsSync(envFile), false);
    const uninstallCommands = readFileSync(log, 'utf8').replaceAll(/\.cmd/gi, '');
    assert.match(uninstallCommands, /codex mcp remove hevy-mcp/);
    assert.match(uninstallCommands, /claude mcp remove --scope user hevy-mcp/);
    assert.doesNotMatch(uninstallCommands, /mcp remove (?:--scope user )?hevy(?:\s|$)/);
    assert.doesNotMatch(uninstallCommands, /docker (?:info|pull)/);
  }
  finally {
    rmSync(root, { recursive: true, force: true });
  }
});
