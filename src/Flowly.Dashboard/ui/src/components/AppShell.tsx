import { useState } from 'react';
import { NavLink, useLocation } from 'react-router-dom';
import {
  AppBar, Box, Drawer, IconButton, List, ListItem, ListItemButton,
  ListItemIcon, ListItemText, Toolbar, Tooltip, Typography,
} from '@mui/material';
import MenuIcon from '@mui/icons-material/Menu';
import WorkHistoryIcon from '@mui/icons-material/WorkHistory';
import EventRepeatIcon from '@mui/icons-material/EventRepeat';
import ErrorOutlineIcon from '@mui/icons-material/ErrorOutline';
import SendIcon from '@mui/icons-material/Send';
import { useConfig } from '../hooks/useConfig';
import type { DashboardConfig } from '../types';
import FlowlyIcon from './FlowlyIcon';

const DRAWER_WIDTH = 220;

const ALL_NAV = [
  { label: 'Jobs',           to: '/jobs',           icon: <WorkHistoryIcon />,  visible: (c: DashboardConfig | null) => c?.hasJobs ?? true },
  { label: 'Recurring Jobs', to: '/recurring-jobs', icon: <EventRepeatIcon />,  visible: (c: DashboardConfig | null) => c?.hasJobs ?? true },
  { label: 'Dead Letters',   to: '/dead-letters',   icon: <ErrorOutlineIcon />, visible: (c: DashboardConfig | null) => c?.hasDeadLetters ?? true },
  { label: 'Submit',         to: '/submit',          icon: <SendIcon />,         visible: (c: DashboardConfig | null) => c?.hasSubmitters ?? true },
];

export default function AppShell({ children }: { children: React.ReactNode }) {
  const [mobileOpen, setMobileOpen] = useState(false);
  const location = useLocation();
  const config = useConfig();
  const NAV = ALL_NAV.filter(item => item.visible(config));

  const activeLabel = NAV.find((n) => location.hash.startsWith(`#${n.to}`))?.label ?? 'Dashboard';

  const drawer = (
    <Box>
      <Toolbar sx={{ gap: 1 }}>
        <FlowlyIcon size={28} />
        <Typography variant="h6" fontWeight={700} color="primary">Flowly</Typography>
      </Toolbar>
      <List disablePadding>
        {NAV.map((item) => (
          <ListItem key={item.to} disablePadding>
            <ListItemButton
              component={NavLink}
              to={item.to}
              onClick={() => setMobileOpen(false)}
              sx={{
                mx: 1, borderRadius: 1,
                '&.active': {
                  backgroundColor: 'primary.main', color: 'white',
                  '& .MuiListItemIcon-root': { color: 'white' },
                  '&:hover': { backgroundColor: 'primary.dark' },
                },
              }}
            >
              <ListItemIcon sx={{ minWidth: 36 }}>{item.icon}</ListItemIcon>
              <ListItemText primary={item.label} />
            </ListItemButton>
          </ListItem>
        ))}
      </List>
    </Box>
  );

  return (
    <Box sx={{ display: 'flex', minHeight: '100vh' }}>
      <AppBar
        position="fixed"
        elevation={0}
        sx={{
          width: { md: `calc(100% - ${DRAWER_WIDTH}px)` },
          ml: { md: `${DRAWER_WIDTH}px` },
          borderBottom: '1px solid', borderColor: 'divider',
          backgroundColor: 'background.paper', color: 'text.primary',
        }}
      >
        <Toolbar>
          <Tooltip title="Menu">
            <IconButton edge="start" onClick={() => setMobileOpen(true)} sx={{ mr: 2, display: { md: 'none' } }}>
              <MenuIcon />
            </IconButton>
          </Tooltip>
          <Typography variant="h6" fontWeight={600}>{activeLabel}</Typography>
        </Toolbar>
      </AppBar>

      <Box component="nav" sx={{ width: { md: DRAWER_WIDTH }, flexShrink: { md: 0 } }}>
        <Drawer
          variant="temporary" open={mobileOpen} onClose={() => setMobileOpen(false)}
          ModalProps={{ keepMounted: true }}
          sx={{ display: { xs: 'block', md: 'none' }, '& .MuiDrawer-paper': { width: DRAWER_WIDTH } }}
        >
          {drawer}
        </Drawer>
        <Drawer
          variant="permanent"
          sx={{ display: { xs: 'none', md: 'block' }, '& .MuiDrawer-paper': { width: DRAWER_WIDTH, boxSizing: 'border-box', borderRight: '1px solid', borderColor: 'divider' } }}
          open
        >
          {drawer}
        </Drawer>
      </Box>

      <Box
        component="main"
        sx={{ flexGrow: 1, p: 3, width: { md: `calc(100% - ${DRAWER_WIDTH}px)` }, backgroundColor: 'background.default', minHeight: '100vh' }}
      >
        <Toolbar />
        {children}
      </Box>
    </Box>
  );
}
