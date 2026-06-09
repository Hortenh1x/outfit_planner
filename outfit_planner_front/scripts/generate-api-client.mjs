import { spawnSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const frontendRoot = path.resolve(scriptDir, '..');
const repoRoot = path.resolve(frontendRoot, '..');
const apiProject = path.join(repoRoot, 'outfit_planner_back', 'src', 'OutfitPlanner.Api', 'OutfitPlanner.Api.csproj');
const openApiCache = path.join(
  repoRoot,
  'outfit_planner_back',
  'src',
  'OutfitPlanner.Api',
  'obj',
  'OutfitPlanner.Api.OpenApiFiles.cache'
);
const openApiDir = path.join(frontendRoot, '.generated', 'openapi');
const schemaOutput = path.join(frontendRoot, 'src', 'api', 'generated', 'schema.ts');
const openApiTypescriptBin = path.join(
  frontendRoot,
  'node_modules',
  '.bin',
  process.platform === 'win32' ? 'openapi-typescript.cmd' : 'openapi-typescript'
);
const openApiTypescriptBinForShell = path.join(
  frontendRoot,
  'node_modules',
  'openapi-typescript',
  'bin',
  'cli.js'
);

fs.mkdirSync(openApiDir, { recursive: true });
fs.mkdirSync(path.dirname(schemaOutput), { recursive: true });
fs.rmSync(openApiCache, { force: true });

run('dotnet', [
  'build',
  apiProject,
  '/p:OpenApiGenerateDocuments=true',
  `/p:OpenApiDocumentsDirectory=${openApiDir}`
]);

const openApiDocument = newestJsonFile(openApiDir);

runOpenApiTypescript(openApiDocument, schemaOutput);

function newestJsonFile(directory) {
  const files = fs
    .readdirSync(directory, { withFileTypes: true })
    .filter((entry) => entry.isFile() && entry.name.endsWith('.json'))
    .map((entry) => {
      const fullPath = path.join(directory, entry.name);
      return {
        fullPath,
        modifiedAt: fs.statSync(fullPath).mtimeMs
      };
    })
    .sort((left, right) => right.modifiedAt - left.modifiedAt);

  if (files.length === 0) {
    throw new Error(`No generated OpenAPI JSON document found in ${directory}`);
  }

  return files[0].fullPath;
}

function run(command, args, options = {}) {
  const result = spawnSync(command, args, {
    cwd: frontendRoot,
    stdio: 'inherit',
    ...options
  });

  if (result.error) {
    throw result.error;
  }

  if (result.status !== 0) {
    throw new Error(`${command} exited with status ${result.status}`);
  }
}

function runOpenApiTypescript(input, output) {
  if (process.platform === 'win32') {
    run(process.execPath, [openApiTypescriptBinForShell, input, '--output', output]);
    return;
  }

  run(openApiTypescriptBin, [input, '--output', output]);
}
