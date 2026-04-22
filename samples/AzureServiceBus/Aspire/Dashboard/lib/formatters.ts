export function formatDate(iso: string | null): string {
  if (!iso) return '—';
  return new Date(iso).toLocaleString();
}

export function duration(started: string | null, completed: string | null): string {
  if (!started) return '—';
  const end = completed ? new Date(completed) : new Date();
  const ms = end.getTime() - new Date(started).getTime();
  const s = Math.floor(ms / 1000);
  if (s < 60) return `${s}s`;
  const m = Math.floor(s / 60);
  const rem = s % 60;
  return `${m}m ${rem}s`;
}
