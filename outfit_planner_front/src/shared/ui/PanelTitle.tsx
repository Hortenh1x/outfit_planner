import type { CSSProperties, ReactNode } from 'react';

const headingStyle: CSSProperties = { fontFamily: 'Nunito, sans-serif' };

export function PanelTitle({ icon, title }: { icon: ReactNode; title: string }) {
  return (
    <div className="panel-title">
      <span>{icon}</span>
      <h2 style={headingStyle}>{title}</h2>
    </div>
  );
}
