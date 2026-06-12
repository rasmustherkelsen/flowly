import { useEffect, useState } from 'react';
import type { DashboardConfig } from '../types';

let cached: DashboardConfig | null = null;

export function useConfig() {
  const [config, setConfig] = useState<DashboardConfig | null>(cached);

  useEffect(() => {
    if (cached) return;
    fetch('api/config')
      .then(r => r.json())
      .then((d: DashboardConfig) => { cached = d; setConfig(d); })
      .catch(() => {});
  }, []);

  return config;
}
