import type { CSSProperties } from 'react';

const headingStyle: CSSProperties = { fontFamily: 'Nunito, sans-serif' };

export function MetricOrb({ label, value, tone }: { label: string; value: number; tone: 'violet' | 'blue' | 'pink' }) {
  return (
    <div className={`metric-orb ${tone}`}>
      <strong style={headingStyle}>{value}</strong>
      <span>{label}</span>
    </div>
  );
}
