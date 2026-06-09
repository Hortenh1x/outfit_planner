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
});
