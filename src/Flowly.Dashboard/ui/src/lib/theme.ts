import { createTheme } from '@mui/material/styles';

export function createAppTheme(mode: 'light' | 'dark') {
  return createTheme({
    palette: {
      mode,
      primary: { main: '#1976d2' },
      background: {
        default: mode === 'light' ? '#f5f7fa' : '#0f172a',
        paper:   mode === 'light' ? '#ffffff'  : '#1e293b',
      },
    },
    shape: { borderRadius: 8 },
    components: {
      MuiCard: {
        styleOverrides: {
          root: { boxShadow: '0 1px 3px rgba(0,0,0,0.08), 0 1px 2px rgba(0,0,0,0.06)' },
        },
      },
      MuiTableHead: {
        styleOverrides: {
          root: {
            '& .MuiTableCell-head': {
              fontWeight: 600,
              backgroundColor: mode === 'light' ? '#f8fafc' : '#1e293b',
              borderBottom: `2px solid ${mode === 'light' ? '#e2e8f0' : '#334155'}`,
            },
          },
        },
      },
    },
  });
}
