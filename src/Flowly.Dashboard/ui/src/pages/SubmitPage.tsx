import { useCallback, useEffect, useState } from 'react';
import {
  Alert, Box, Button, Card, CardContent, CircularProgress, Chip,
  FormControl, InputLabel, MenuItem, Select, TextField, Typography,
} from '@mui/material';
import SendIcon from '@mui/icons-material/Send';
import type { SubmitterInfo } from '../types';

interface JsonSchemaProperty {
  type?: string | string[];
  format?: string;
  description?: string;
}

interface JsonSchema {
  properties?: Record<string, JsonSchemaProperty>;
  required?: string[];
}

function buildDefaultPayload(schemaStr: string): Record<string, unknown> {
  try {
    const schema: JsonSchema = JSON.parse(schemaStr);
    const result: Record<string, unknown> = {};
    for (const [key, prop] of Object.entries(schema.properties ?? {})) {
      const typeArray = Array.isArray(prop.type) ? prop.type : prop.type ? [prop.type] : [];
      const type = typeArray.find(t => t !== 'null' && t !== 'string')
        ?? typeArray.find(t => t !== 'null')
        ?? typeArray[0];
      result[key] = type === 'number' || type === 'integer' ? 1
        : type === 'boolean' ? false
        : type === 'array' ? []
        : prop.format === 'uuid' ? crypto.randomUUID()
        : prop.format === 'date-time' ? new Date().toISOString()
        : type === 'string' ? 'Some Text Value'
        : '';
    }
    return result;
  } catch {
    return {};
  }
}

export default function SubmitPage() {
  const [submitters, setSubmitters] = useState<SubmitterInfo[] | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [selected, setSelected] = useState('');
  const [payload, setPayload] = useState('{}');
  const [payloadError, setPayloadError] = useState<string | null>(null);
  const [count, setCount] = useState(1);
  const [submitting, setSubmitting] = useState(false);
  const [result, setResult] = useState<{ ok: boolean; message: string } | null>(null);
  const [response, setResponse] = useState<string | null>(null);

  useEffect(() => {
    fetch('api/submitters')
      .then((r) => r.json())
      .then((data: SubmitterInfo[]) => {
        setSubmitters(data);
        if (data.length > 0) {
          setSelected(data[0].typeName);
          setPayload(JSON.stringify(buildDefaultPayload(data[0].schema), null, 2));
        }
      })
      .catch((e) => setLoadError(e instanceof Error ? e.message : 'Failed to load submitters'));
  }, []);

  const handleTypeChange = useCallback((typeName: string) => {
    setSelected(typeName);
    setResult(null);
    setResponse(null);
    setPayloadError(null);
    const info = submitters?.find((s) => s.typeName === typeName);
    if (info) {
      setPayload(JSON.stringify(buildDefaultPayload(info.schema), null, 2));
      if (info.kind === 'call') setCount(1);
    }
  }, [submitters]);

  const handleSubmit = async () => {
    setPayloadError(null);
    let parsed: unknown;
    try {
      parsed = JSON.parse(payload);
    } catch {
      setPayloadError('Invalid JSON payload.');
      return;
    }

    setSubmitting(true);
    setResult(null);
    setResponse(null);
    try {
      const res = await fetch('api/submit', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ type: selected, payload: parsed, count }),
      });
      if (res.ok) {
        const text = await res.text();
        const info = submitters?.find((s) => s.typeName === selected);
        const isCall = info?.kind === 'call';
        const isJob = info?.kind === 'job';
        if ((isCall || (isJob && count === 1)) && text) setResponse(text);
        setResult({
          ok: true,
          message: isCall ? 'Call completed.'
            : isJob ? (count === 1 ? 'Job queued successfully.' : `${count} jobs queued successfully.`)
            : count === 1 ? 'Message sent successfully.'
            : `${count} messages sent successfully.`,
        });
      } else if (res.status === 408) {
        setResult({ ok: false, message: 'The call timed out — the handler did not respond within the configured timeout period.' });
      } else {
        const body = await res.text().catch(() => `HTTP ${res.status}`);
        setResult({ ok: false, message: body || `HTTP ${res.status}` });
      }
    } catch (e) {
      setResult({ ok: false, message: e instanceof Error ? e.message : 'Submission failed.' });
    } finally {
      setSubmitting(false);
    }
  };

  if (loadError) return <Alert severity="error">{loadError}</Alert>;
  if (!submitters) return <Box sx={{ display: 'flex', justifyContent: 'center', p: 6 }}><CircularProgress /></Box>;

  if (submitters.length === 0) {
    return (
      <Box>
        <Typography variant="h5" sx={{ fontWeight: 700, mb: 3 }}>Submit</Typography>
        <Alert severity="info">No message submitters are registered in this application.</Alert>
      </Box>
    );
  }

  const currentInfo = submitters.find((s) => s.typeName === selected);

  return (
    <Box>
      <Typography variant="h5" sx={{ fontWeight: 700, mb: 3 }}>Submit</Typography>

      <Card sx={{ maxWidth: 600 }}>
        <CardContent sx={{ display: 'flex', flexDirection: 'column', gap: 3 }}>
          <FormControl fullWidth size="small">
            <InputLabel>Message Type</InputLabel>
            <Select value={selected} label="Message Type" onChange={(e) => handleTypeChange(e.target.value)}>
              {submitters.map((s) => (
                <MenuItem key={s.typeName} value={s.typeName}>
                  <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                    {s.typeName}
                    <Chip label={s.kind} size="small" variant="outlined" sx={{ textTransform: 'capitalize' }} />
                  </Box>
                </MenuItem>
              ))}
            </Select>
          </FormControl>

          {currentInfo && (
            <Typography variant="caption" color="text.secondary">
              → {currentInfo.queueOrTopic}
            </Typography>
          )}

          <TextField
            label="Payload (JSON)"
            multiline
            minRows={6}
            maxRows={16}
            fullWidth
            size="small"
            value={payload}
            onChange={(e) => { setPayload(e.target.value); setPayloadError(null); }}
            error={!!payloadError}
            helperText={payloadError}
            slotProps={{ htmlInput: { style: { fontFamily: 'monospace', fontSize: '0.85rem' } } }}
          />

          {currentInfo?.kind !== 'call' && (
            <TextField
              label="Count"
              type="number"
              size="small"
              sx={{ width: 140 }}
              value={count}
              onChange={(e) => setCount(Math.min(1000, Math.max(1, parseInt(e.target.value, 10) || 1)))}
              slotProps={{ htmlInput: { min: 1, max: 1000 } }}
              helperText="Messages to send (max 1000)"
            />
          )}

          {result && <Alert severity={result.ok ? 'success' : 'error'}>{result.message}</Alert>}

          {response && (
            <Box>
              <Typography variant="overline" color="text.secondary" sx={{ display: 'block' }} gutterBottom>Response</Typography>
              <Box component="pre" sx={{ fontFamily: 'monospace', fontSize: '0.8rem', whiteSpace: 'pre-wrap', bgcolor: 'action.hover', borderRadius: 1, p: 1.5, m: 0 }}>
                {(() => { try { return JSON.stringify(JSON.parse(response), null, 2); } catch { return response; } })()}
              </Box>
            </Box>
          )}

          <Button
            variant="contained"
            startIcon={submitting ? <CircularProgress size={16} color="inherit" /> : <SendIcon />}
            onClick={handleSubmit}
            disabled={submitting}
          >
            {currentInfo?.kind === 'call' ? 'Call' : currentInfo?.kind === 'job' ? 'Queue Job' : 'Send'}
          </Button>
        </CardContent>
      </Card>
    </Box>
  );
}
