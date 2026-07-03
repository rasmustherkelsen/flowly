import { useConfig } from './useConfig';

export function useCanSubmit(): boolean {
  const config = useConfig();
  return !(config?.authEnabled && config.canSubmit === false);
}
