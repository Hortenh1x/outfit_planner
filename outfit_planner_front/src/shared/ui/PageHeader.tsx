import type { CSSProperties } from 'react';

const headingStyle: CSSProperties = { fontFamily: 'Nunito, sans-serif' };

export function PageHeader({ eyebrow, title, text }: { eyebrow: string; title: string; text: string }) {
  return (
    <header className="page-header">
      <div>
        <p>{eyebrow}</p>
        <h1 style={headingStyle}>{title}</h1>
      </div>
      <span>{text}</span>
    </header>
  );
}
