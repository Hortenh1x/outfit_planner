export function MetricOrb({ label, value, tone }: { label: string; value: number; tone: 'violet' | 'blue' | 'pink' }) {
  return (
    <div className={`metric-orb ${tone}`}>
      <strong>{value}</strong>
      <span>{label}</span>
    </div>
  );
}
