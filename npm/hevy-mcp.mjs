#!/usr/bin/env node

import { spawnSync } from 'node:child_process';
import {
  chmodSync,
  existsSync,
  mkdirSync,
  rmSync,
  writeFileSync,
} from 'node:fs';
import { homedir } from 'node:os';
import { delimiter, dirname, extname, join } from 'node:path';
import { pathToFileURL } from 'node:url';
import { createInterface } from 'node:readline/promises';

const IMAGE = 'ghcr.io/imhalawa/hevy-mcp:0.1.2';
const SERVER_NAME = 'hevy-mcp';
const IS_WINDOWS = process.platform === 'win32';

export const isWindowsNodeFromWsl = (platform = process.platform, cwd = process.cwd()) =>
  platform === 'win32' && /^\\\\wsl(?:\.localhost|\$)\\/i.test(cwd);

const clients = [
  {
    name: 'Codex',
    command: 'codex',
    remove: ['mcp', 'remove', SERVER_NAME],
    add: (dockerCommand, dockerArguments) => [
      'mcp', 'add', SERVER_NAME, '--', dockerCommand, ...dockerArguments,
    ],
  },
  {
    name: 'Claude Code',
    command: 'claude',
    remove: ['mcp', 'remove', '--scope', 'user', SERVER_NAME],
    add: (dockerCommand, dockerArguments) => [
      'mcp', 'add', '--transport', 'stdio', '--scope', 'user', SERVER_NAME,
      '--', dockerCommand, ...dockerArguments,
    ],
  },
];

const executableExtensions = () => IS_WINDOWS
  ? (process.env.PATHEXT ?? '.COM;.EXE;.BAT;.CMD').split(';')
  : [''];

const findCommand = (command) => {
  const directories = (process.env.PATH ?? '').split(delimiter).filter(Boolean);
  const candidates = directories.flatMap((directory) =>
    executableExtensions().map((extension) => join(directory, `${command}${extension}`)));
  return candidates.find(existsSync);
};

const isWindowsScript = (command) => IS_WINDOWS && ['.bat', '.cmd'].includes(extname(command).toLowerCase());

const run = (command, arguments_, stdio = 'inherit') => isWindowsScript(command)
  ? spawnSync('powershell.exe', [
    '-NoLogo', '-NoProfile', '-NonInteractive', '-Command',
    '& $env:HEVY_MCP_EXECUTABLE @args', ...arguments_,
  ], {
    encoding: 'utf8',
    env: { ...process.env, HEVY_MCP_EXECUTABLE: command },
    stdio,
  })
  : spawnSync(command, arguments_, { encoding: 'utf8', stdio });

const environmentFile = () => IS_WINDOWS
  ? join(process.env.APPDATA ?? join(homedir(), 'AppData', 'Roaming'), 'hevy-mcp', 'hevy.env')
  : join(process.env.XDG_CONFIG_HOME ?? join(homedir(), '.config'), 'hevy-mcp', 'hevy.env');

export const validateApiKey = (apiKey) => {
  if (!apiKey?.trim()) throw new Error('A non-empty Hevy API key is required.');
  if (/\r|\n|\0/.test(apiKey)) throw new Error('The Hevy API key contains an invalid line break.');
  return apiKey.trim();
};

const promptSecret = (label) => new Promise((resolve, reject) => {
  if (!process.stdin.isTTY || !process.stdin.setRawMode) {
    reject(new Error('Run setup in an interactive terminal or set HEVY_API_KEY for this command.'));
    return;
  }

  let value = '';
  const finish = (error) => {
    process.stdin.off('data', onData);
    process.stdin.setRawMode(false);
    process.stdin.pause();
    process.stdout.write('\n');
    error ? reject(error) : resolve(value);
  };
  const onData = (input) => {
    for (const character of input) {
      if (character === '\u0003') return finish(new Error('Setup cancelled.'));
      if (character === '\r' || character === '\n') return finish();
      if (character === '\u007f' || character === '\b') value = value.slice(0, -1);
      else if (character >= ' ') value += character;
    }
  };

  process.stdout.write(label);
  process.stdin.setEncoding('utf8');
  process.stdin.setRawMode(true);
  process.stdin.resume();
  process.stdin.on('data', onData);
});

const askToEnableWrites = async () => {
  const terminal = createInterface({ input: process.stdin, output: process.stdout });
  const answer = await terminal.question('Allow your AI clients to change Hevy data? [y/N] ');
  terminal.close();
  return /^y(?:es)?$/i.test(answer.trim());
};

const lockWindowsFile = (path) => {
  const identity = run('whoami.exe', [], 'pipe');
  const account = identity.stdout?.trim();
  if (identity.status !== 0 || !account) {
    rmSync(path, { force: true });
    throw new Error('Windows could not identify the account that owns the API key.');
  }
  const result = run('icacls.exe', [path, '/inheritance:r', '/grant:r', `${account}:(F)`], 'ignore');
  if (result.status === 0) return;
  rmSync(path, { force: true });
  throw new Error('Windows could not restrict the API key file to your account.');
};

const saveApiKey = (apiKey) => {
  const path = environmentFile();
  mkdirSync(dirname(path), { recursive: true, mode: 0o700 });
  writeFileSync(path, `HEVY_API_KEY=${apiKey}\n`, { encoding: 'utf8', mode: 0o600 });
  IS_WINDOWS ? lockWindowsFile(path) : chmodSync(path, 0o600);
  return path;
};

export const dockerArguments = (envFile, allowWrites) => [
  'run',
  '--rm',
  '-i',
  '--read-only',
  '--tmpfs',
  '/tmp:rw,noexec,nosuid,size=16m',
  '--env-file',
  envFile,
  ...(allowWrites ? [] : ['-e', 'HEVY_READ_ONLY=true']),
  IMAGE,
];

export const addArguments = (clientName, dockerCommand, arguments_) =>
  clients.find(({ name }) => name === clientName)?.add(dockerCommand, arguments_);

const checkDocker = (dockerCommand) => {
  const result = run(dockerCommand, ['info'], 'ignore');
  if (result.status !== 0) throw new Error('Docker is installed but is not running. Start Docker and try again.');
};

const pullImage = (dockerCommand) => {
  console.log(`Pulling ${IMAGE}...`);
  const result = run(dockerCommand, ['pull', IMAGE]);
  if (result.status !== 0) throw new Error('Docker could not pull the hevy-mcp image.');
};

const configureClient = (client, executable, dockerCommand, arguments_) => {
  run(executable, client.remove, 'ignore');
  const result = run(executable, client.add(dockerCommand, arguments_));
  if (result.status !== 0) return client.name;
  console.log(`Configured ${client.name}.`);
  return undefined;
};

const usage = () => console.log(`Usage: hevy-mcp setup|uninstall

Prompts for your Hevy API key, stores it in a user-only file, pulls the Docker
image, and configures installed Codex and Claude Code clients. Uninstall removes
only the hevy-mcp registrations and saved API key.`);

const setup = async () => {
  const dockerCommand = findCommand('docker');
  if (!dockerCommand) throw new Error('Docker is required. Install Docker Desktop and try again.');
  checkDocker(dockerCommand);

  const detectedClients = clients
    .map((client) => ({ client, executable: findCommand(client.command) }))
    .filter(({ executable }) => executable);
  if (detectedClients.length === 0) {
    throw new Error('No supported MCP client found. Install Codex or Claude Code and try again.');
  }

  console.log(`Found ${detectedClients.map(({ client }) => client.name).join(', ')}.`);
  const apiKey = validateApiKey(process.env.HEVY_API_KEY ?? await promptSecret('Hevy API key: '));
  const allowWrites = await askToEnableWrites();
  const envFile = saveApiKey(apiKey);
  pullImage(dockerCommand);

  const arguments_ = dockerArguments(envFile, allowWrites);
  const failures = detectedClients
    .map(({ client, executable }) => configureClient(client, executable, dockerCommand, arguments_))
    .filter(Boolean);
  if (failures.length > 0) throw new Error(`Could not configure: ${failures.join(', ')}.`);

  console.log(`\nDone. Restart your AI client, then ask: “Show my five most recent Hevy workouts.”`);
};

const uninstall = () => {
  const detectedClients = clients
    .map((client) => ({ client, executable: findCommand(client.command) }))
    .filter(({ executable }) => executable);
  if (detectedClients.length === 0) {
    throw new Error('No supported MCP client found. Nothing was removed.');
  }

  const failures = detectedClients
    .filter(({ client, executable }) => run(executable, client.remove).status !== 0)
    .map(({ client }) => client.name);
  if (failures.length > 0) throw new Error(`Could not remove from: ${failures.join(', ')}.`);

  rmSync(environmentFile(), { force: true });
  console.log('Removed hevy-mcp and its saved API key.');
};

const main = async () => {
  const command = process.argv[2] ?? 'setup';
  if (['--help', '-h', 'help'].includes(command)) return usage();
  if (!['setup', 'uninstall'].includes(command)) throw new Error(`Unknown command: ${command}`);
  if (isWindowsNodeFromWsl()) {
    throw new Error('Windows Node.js was launched from WSL. Install Node.js inside WSL, then run this command again so it targets the Linux MCP client.');
  }
  command === 'setup' ? await setup() : uninstall();
};

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main().catch((error) => {
    console.error(`hevy-mcp: ${error.message}`);
    process.exitCode = 1;
  });
}
