import { useCallback, useEffect, useState } from 'react';

export function useAutoRefresh<T>(
  fetchFn: () => Promise<T>,
  intervalMs: number
) {
  const [data, setData] = useState<T | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async (showSpinner = false) => {
    if (showSpinner) setLoading(true);
    setError(null);
    try {
      setData(await fetchFn());
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to load');
    } finally {
      setLoading(false);
    }
  }, [fetchFn]);

  useEffect(() => {
    load(true);
    const id = setInterval(() => load(), intervalMs);
    return () => clearInterval(id);
  }, [load, intervalMs]);

  const refresh = useCallback(() => load(true), [load]);

  return { data, loading, error, refresh };
}
