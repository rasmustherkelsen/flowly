'use client';

import {
  Box,
  FormControl,
  IconButton,
  InputLabel,
  MenuItem,
  Select,
  Tooltip,
  Typography,
} from '@mui/material';
import RefreshIcon from '@mui/icons-material/Refresh';

interface PageHeaderProps {
  title: string;
  filterValue: string;
  filterOptions: string[];
  onFilterChange: (value: string) => void;
  loading: boolean;
  onRefresh: () => void;
  totalCount?: number;
  children?: React.ReactNode;
}

export default function PageHeader({
  title,
  filterValue,
  filterOptions,
  onFilterChange,
  loading,
  onRefresh,
  totalCount,
  children,
}: PageHeaderProps) {
  return (
    <Box sx={{ display: 'flex', alignItems: 'center', mb: 3, gap: 2, flexWrap: 'wrap' }}>
      <Typography variant="h5" fontWeight={700} sx={{ flexGrow: 1 }}>
        {title}
      </Typography>
      {children}
      <FormControl size="small" sx={{ minWidth: 160 }}>
        <InputLabel>Status</InputLabel>
        <Select value={filterValue} label="Status" onChange={(e) => onFilterChange(e.target.value)}>
          {filterOptions.map((s) => (
            <MenuItem key={s} value={s}>{s || 'All'}</MenuItem>
          ))}
        </Select>
      </FormControl>
      <Tooltip title="Refresh">
        <span>
          <IconButton onClick={onRefresh} disabled={loading}>
            <RefreshIcon />
          </IconButton>
        </span>
      </Tooltip>
      {!loading && totalCount !== undefined && (
        <Typography variant="body2" color="text.secondary">
          {totalCount} total
        </Typography>
      )}
    </Box>
  );
}
