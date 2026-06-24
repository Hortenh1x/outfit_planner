import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';

const stylesCss = readFileSync(join(process.cwd(), 'src', 'styles.css'), 'utf8');
const shellCss = readFileSync(join(process.cwd(), 'src', 'app', 'editorialShell.css'), 'utf8');
const wardrobeCss = readFileSync(join(process.cwd(), 'src', 'features', 'wardrobe', 'wardrobe.css'), 'utf8');

describe('Crimson Plinth design tokens', () => {
  it('uses the light theme reference palette and typography', () => {
    expect(stylesCss).toContain('--font-body: "Inter Tight"');
    expect(stylesCss).toContain('--font-display: "Instrument Serif"');
    expect(stylesCss).toContain('--canvas: #F6F2EC');
    expect(stylesCss).toContain('--surface: #FFFFFF');
    expect(stylesCss).toContain('--surface-muted: #EFE9E0');
    expect(stylesCss).toContain('--ink: #1A1614');
    expect(stylesCss).toContain('--muted: #6B645D');
    expect(stylesCss).toContain('--hairline: #E5DFD6');
    expect(stylesCss).toContain('--hairline-strong: #D6CFC3');
    expect(stylesCss).toContain('--accent: #F00045');
    expect(stylesCss).toContain('--accent-strong: #A30037');
    expect(stylesCss).toContain('--accent-edge: #520019');
    expect(stylesCss).toContain('--accent-soft: #FEF0F4');
  });

  it('keeps primary actions pink in the dark theme', () => {
    const darkRootBlock = stylesCss.match(/:root\[data-theme="dark"\]\s*\{(?<body>[\s\S]*?)\n\}/)?.groups?.body ?? '';
    const darkShellBlock = shellCss.match(/\.editorial-shell\[data-theme="dark"\]\s*\{(?<body>[\s\S]*?)\n\}/)?.groups?.body ?? '';

    expect(darkRootBlock).toContain('--accent: #F00045');
    expect(darkRootBlock).toContain('--accent-strong: #A30037');
    expect(darkRootBlock).toContain('--accent-edge: #520019');
    expect(darkShellBlock).toContain('--editorial-accent: #F00045');
    expect(darkShellBlock).toContain('--editorial-accent-strong: #A30037');
    expect(darkShellBlock).toContain('--editorial-accent-edge: #520019');
  });

  it('defines softer button corners and lightweight interaction motion', () => {
    expect(stylesCss).toContain('--radius-button: 14px');
    expect(stylesCss).toContain('--ease-press: cubic-bezier(0.3, 0.7, 0.4, 1)');
    expect(stylesCss).toContain('--ease-press-snap: cubic-bezier(0.3, 0.7, 0.4, 1.5)');
    expect(stylesCss).toContain('box-shadow: 0 4px 0 var(--accent-strong), 0 6px 0 var(--accent-edge)');
    expect(stylesCss).toContain('transform: translateY(-6px)');
    expect(wardrobeCss).toContain('border-radius: var(--radius-button)');
  });
});

describe('Editorial responsive layout rules', () => {
  it('moves the account shell below page content on mobile', () => {
    expect(shellCss).toMatch(/@media \(max-width: 920px\)[\s\S]*\.editorial-main-panel\s*\{[\s\S]*order: 1/);
    expect(shellCss).toMatch(/@media \(max-width: 920px\)[\s\S]*\.editorial-sidebar\s*\{[\s\S]*order: 2/);
  });

  it('keeps creation and planning controls first on mobile', () => {
    expect(wardrobeCss).toMatch(/@media \(max-width: 760px\)[\s\S]*\.wardrobe-rail,\s*\.wardrobe-rail-form\s*\{[\s\S]*order: -1/);
    expect(stylesCss).toMatch(/@media \(max-width: 760px\)[\s\S]*\.calendar-plan-rail\s*\{[\s\S]*order: -1/);
  });

  it('keeps the selected current calendar day legible', () => {
    expect(stylesCss).toMatch(/\.calendar-day\.today\.selected-day span\s*\{[\s\S]*color: var\(--ink-inverse\)/);
  });

  it('constrains wardrobe upload queue rows to the rail width', () => {
    expect(wardrobeCss).toMatch(/\.wardrobe-rail,\s*\.wardrobe-rail-form\s*\{[\s\S]*max-width: 100%/);
    expect(wardrobeCss).toMatch(/\.upload-queue,\s*\.upload-queue-row\s*\{[\s\S]*min-width: 0/);
    expect(wardrobeCss).toMatch(/\.upload-queue-row\s*\{[\s\S]*grid-template-columns: minmax\(0, 1fr\)/);
    expect(wardrobeCss).toMatch(/\.upload-queue-heading\s*\{[\s\S]*grid-template-columns: minmax\(0, 1fr\) auto/);
    expect(wardrobeCss).toMatch(/\.upload-queue-heading strong\s*\{[\s\S]*overflow-wrap: anywhere/);
  });
});
