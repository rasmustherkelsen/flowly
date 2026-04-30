'use client';

import { useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  CircularProgress,
  FormControl,
  InputLabel,
  MenuItem,
  Select,
  TextField,
  Typography,
} from '@mui/material';
import SendIcon from '@mui/icons-material/Send';

const MESSAGE_TYPES = [
  { value: 'process-order', label: 'Process Order' },
  { value: 'rebuild-index', label: 'Rebuild Index' },
  { value: 'some-query', label: 'Some Query' },
];

export default function SubmitPage() {
  const [messageType, setMessageType] = useState('process-order');
  const [messageCount, setMessageCount] = useState<number | string>(1);
  const [submitting, setSubmitting] = useState(false);
  const [result, setResult] = useState<{ ok: boolean; message: string } | null>(null);

  const handleSubmit = async () => {
    setSubmitting(true);
    setResult(null);
    try {
      const count = Math.max(1, parseInt(String(messageCount), 10) || 1);
      const params = new URLSearchParams({ messageType, messageCount: String(count) });
      const res = await fetch(`/api/submit?${params}`);
      if (res.ok) {
        const label = MESSAGE_TYPES.find((t) => t.value === messageType)?.label ?? messageType;
        setResult({ ok: true, message: `Successfully submitted ${count} ${label} message${count !== 1 ? 's' : ''}.` });
      } else {
        setResult({ ok: false, message: `Submission failed (HTTP ${res.status}).` });
      }
    } catch (e) {
      setResult({ ok: false, message: e instanceof Error ? e.message : 'Submission failed.' });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Box>
      <Typography variant="h5" fontWeight={700} mb={3}>
        Submit Messages
      </Typography>

      <Card sx={{ maxWidth: 480 }}>
        <CardContent sx={{ display: 'flex', flexDirection: 'column', gap: 3 }}>
          <FormControl fullWidth size="small">
            <InputLabel>Message Type</InputLabel>
            <Select
              value={messageType}
              label="Message Type"
              onChange={(e) => setMessageType(e.target.value)}
            >
              {MESSAGE_TYPES.map((t) => (
                <MenuItem key={t.value} value={t.value}>{t.label}</MenuItem>
              ))}
            </Select>
          </FormControl>

          <TextField
            label="Number of Messages"
            type="number"
            size="small"
            value={messageCount}
            onChange={(e) => setMessageCount(e.target.value)}
            onBlur={(e) => setMessageCount(Math.max(1, parseInt(e.target.value, 10) || 1))}
            slotProps={{ htmlInput: { min: 1 } }}
          />

          {result && (
            <Alert severity={result.ok ? 'success' : 'error'}>{result.message}</Alert>
          )}

          <Button
            variant="contained"
            startIcon={submitting ? <CircularProgress size={16} color="inherit" /> : <SendIcon />}
            onClick={handleSubmit}
            disabled={submitting}
          >
            Submit
          </Button>
        </CardContent>
      </Card>
    </Box>
  );
}
