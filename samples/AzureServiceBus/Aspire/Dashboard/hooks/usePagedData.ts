'use client';

import { useCallback, useEffect, useState } from 'react';
import type { PagedResult } from '@/types';

const PAGE_SIZE = 20;

export function usePagedData<T>(
  buildUrl: (page: number, filter: string) => string,
  autoRefreshMs: number
) {
  const [result, setResult] = useState<PagedResult<T> | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [page, setPage] = useState(1);
  const [filter, setFilter] = useState('');

  const load = useCallback(async (p: number, f: string, showSpinner = false) => {
    if (showSpinner) setLoading(true);
    setError(null);
    try {
      const res = await fetch(buildUrl(p, f));
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      setResult(await res.json());
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to load data');
    } finally {
      setLoading(false);
    }
  }, [buildUrl]);

  useEffect(() => {
    load(page, filter, true);
    const interval = setInterval(() => load(page, filter), autoRefreshMs);
    return () => clearInterval(interval);
  }, [load, page, filter, autoRefreshMs]);

  const onFilterChange = useCallback((f: string) => {
    setFilter(f);
    setPage(1);
  }, []);

  const refresh = useCallback(() => load(page, filter, true), [load, page, filter]);

  return {
    result,
    loading,
    error,
    page,
    setPage,
    filter,
    onFilterChange,
    refresh,
    totalPages: result ? Math.ceil(result.totalCount / PAGE_SIZE) : 0,
  };
}
