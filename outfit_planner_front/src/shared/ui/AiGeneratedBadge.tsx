// EU AI Act Art 50(4) transparency: try-on renders are AI-manipulated images of a real
// person, so every surface that shows one carries this visible disclosure — the public
// share page included. Keep it next to generated previews only, never on the composed
// figure (that one is not AI output).
export function AiGeneratedBadge() {
  return <span className="ai-generated-badge">AI-generated</span>;
}
