import { describe, expect, it } from 'vitest';
import fs from 'node:fs';
import path from 'node:path';

const frontendRoot = path.resolve(__dirname, '../../..');
const repoRoot = path.resolve(frontendRoot, '..');

describe('generated API workflow', () => {
  it('keeps generated OpenAPI and schema artifacts ignored', () => {
    const gitignore = fs.readFileSync(path.join(repoRoot, '.gitignore'), 'utf8');

    expect(gitignore).toContain('outfit_planner_front/.generated/');
    expect(gitignore).toContain('outfit_planner_front/src/api/generated/schema.ts');
  });

  it('exposes generation scripts before test and build', () => {
    const packageJson = JSON.parse(fs.readFileSync(path.join(frontendRoot, 'package.json'), 'utf8')) as {
      scripts: Record<string, string>;
      devDependencies: Record<string, string>;
    };

    expect(packageJson.devDependencies['openapi-typescript']).toBeDefined();
    expect(packageJson.scripts['generate:api']).toBe('node scripts/generate-api-client.mjs');
    expect(packageJson.scripts.pretest).toBe('npm run generate:api');
    expect(packageJson.scripts.prebuild).toBe('npm run generate:api');
  });

  it('generates response body content for API types consumed by the frontend', () => {
    const schema = fs.readFileSync(path.join(frontendRoot, 'src/api/generated/schema.ts'), 'utf8');

    expect(schema).toContain('"application/json": components["schemas"]["BodyReferencePhoto"][];');
    expect(schema).toContain('"application/json": components["schemas"]["BodyReferencePhoto"];');
    expect(schema).toContain('"application/json": components["schemas"]["GarmentItem"][];');
    expect(schema).toContain('"application/json": components["schemas"]["GarmentItem"];');
    expect(schema).toContain('"application/json": components["schemas"]["Outfit"][];');
    expect(schema).toContain('"application/json": components["schemas"]["Outfit"];');
    expect(schema).toContain('"application/json": components["schemas"]["TryOnJob"];');
    expect(schema).toContain('"application/json": components["schemas"]["ScheduledOutfit"][];');
    expect(schema).toContain('"application/json": components["schemas"]["ScheduledOutfit"];');
    expect(schema).toContain('"application/json": components["schemas"]["SharedOutfitResponse"];');

    expect(responseSection(schema, '/api/garments', 'get', 200)).not.toContain('content?: never;');
    expect(responseSection(schema, '/api/outfits', 'get', 200)).not.toContain('content?: never;');
    expect(responseSection(schema, '/api/share/{token}', 'get', 200)).not.toContain('content?: never;');
  });
});

function responseSection(schema: string, pathKey: string, operation: string, status: number) {
  const section = operationSection(schema, pathKey, operation);
  const responseStart = section.indexOf(`                ${status}: {`);
  expect(responseStart).toBeGreaterThanOrEqual(0);

  const nextResponseMatch = section
    .slice(responseStart + 1)
    .match(/\n                \d{3}: /);
  const responseEnd = nextResponseMatch
    ? responseStart + 1 + nextResponseMatch.index!
    : section.length;

  return section.slice(responseStart, responseEnd);
}

function operationSection(schema: string, pathKey: string, operation: string) {
  const pathStart = schema.indexOf(`"${pathKey}": {`);
  expect(pathStart).toBeGreaterThanOrEqual(0);

  const nextPathStart = schema.indexOf('\n    "/', pathStart + 1);
  const pathSection = nextPathStart === -1 ? schema.slice(pathStart) : schema.slice(pathStart, nextPathStart);
  const operationStart = pathSection.indexOf(`        ${operation}: {`);
  expect(operationStart).toBeGreaterThanOrEqual(0);

  const nextOperationMatch = pathSection
    .slice(operationStart + 1)
    .match(/\n        (?:get|put|post|delete|options|head|patch|trace)\??: /);
  const operationEnd = nextOperationMatch
    ? operationStart + 1 + nextOperationMatch.index!
    : pathSection.length;

  return pathSection.slice(operationStart, operationEnd);
}
